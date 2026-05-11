namespace PicoDI.Test;

/// <summary>
/// Regression tests for newly-discovered bugs (7, 8, 9). Each test was authored
/// RED-first against the unfixed code path.
/// </summary>
public class AdditionalBugFixRegressionTests
{
    // ------------------------------------------------------------------
    // Bug 7: SvcScope.CreateScope races with parent.DisposeAsync. When
    //        TrackedScopeList.AddToHead loses the race it throws after the
    //        child SvcScope has already been constructed and parented.
    //        Without the fix the orphan child is never disposed.
    //
    //  Deterministic reproduction: seal the parent's child list directly
    //  (mirrors what DisposeAsync does internally) without flipping the
    //  parent's _disposed flag — exactly the gap the bug exploits.
    // ------------------------------------------------------------------

    [Test]
    public async Task CreateScope_AddToHeadFails_PreservesContractAndDisposesCleanly()
    {
        await using var container = new SvcContainer(autoConfigureFromGenerator: false);
        var parent = (SvcScope)container.CreateScope();

        var childScopesField = typeof(SvcScope).GetField(
            "_childScopes",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance
        )!;
        var list = childScopesField.GetValue(parent)!;
        list.GetType()
            .GetMethod(
                "DrainAll",
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance
            )!
            .Invoke(list, null);

        ObjectDisposedException? caught = null;
        try
        {
            parent.CreateScope();
        }
        catch (ObjectDisposedException ex)
        {
            caught = ex;
        }

        // Contract: the AddToHead failure must surface as ObjectDisposedException.
        await Assert.That(caught).IsNotNull();

        // Parent must remain disposable without corruption from the orphan.
        await parent.DisposeAsync();
    }

    // ------------------------------------------------------------------
    // Bug 8: BackgroundSvc.StopAsync uses Task.Delay(Infinite, ct) which
    //        leaks a CancellationTokenRegistration on the *caller's* token
    //        when the executing task completes first. WaitAsync(ct) is the
    //        correct primitive — it cleans up its token registration on
    //        task completion.
    // ------------------------------------------------------------------

    private sealed class FastBackgroundSvc : BackgroundSvc
    {
        private readonly TaskCompletionSource _tcs = new();

        public void Complete() => _tcs.TrySetResult();

        protected override Task ExecuteAsync(CancellationToken stoppingToken) => _tcs.Task;
    }

    [Test]
    public async Task StopAsync_CompletedExecutingTask_DoesNotLeakRegistrationsOnSharedToken()
    {
        using var sharedCts = new CancellationTokenSource();

        long CountRegistrations()
        {
            // .NET 10 layout: CancellationTokenSource._registrations is a
            // CancellationTokenSource.Registrations object whose Callbacks field
            // is the head of a singly-linked list of CallbackNode entries.
            var registrationsField = typeof(CancellationTokenSource).GetField(
                "_registrations",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance
            );
            if (registrationsField is null)
                return -1;

            var registrations = registrationsField.GetValue(sharedCts);
            if (registrations is null)
                return 0;

            var callbacksField = registrations
                .GetType()
                .GetField(
                    "Callbacks",
                    System.Reflection.BindingFlags.NonPublic
                        | System.Reflection.BindingFlags.Public
                        | System.Reflection.BindingFlags.Instance
                );
            if (callbacksField is null)
                return -1;

            long total = 0;
            var node = callbacksField.GetValue(registrations);
            while (node is not null)
            {
                total++;
                var nextField = node.GetType()
                    .GetField(
                        "Next",
                        System.Reflection.BindingFlags.NonPublic
                            | System.Reflection.BindingFlags.Public
                            | System.Reflection.BindingFlags.Instance
                    );
                node = nextField?.GetValue(node);
            }
            return total;
        }

        var before = CountRegistrations();

        for (int i = 0; i < 200; i++)
        {
            var svc = new FastBackgroundSvc();
            await svc.StartAsync(CancellationToken.None);
            svc.Complete();
            await svc.StopAsync(sharedCts.Token);
            await svc.DisposeAsync();
        }

        var after = CountRegistrations();

        // If the runtime exposes the field (before >= 0), assert no growth.
        // With Bug 8 unfixed, after - before == 200; with the fix, it is 0.
        if (before >= 0)
            await Assert.That(after - before).IsLessThanOrEqualTo(2);
    }

    // ------------------------------------------------------------------
    // Bug 9: User-provided singleton instances (RegisterSingle / FromInstance)
    //        are disposed by the container. Microsoft.Extensions.DependencyInjection
    //        does NOT dispose user-provided instances — ownership stays with caller.
    //        Align PicoDI with that contract.
    // ------------------------------------------------------------------

    [Test]
    public async Task RegisterSingle_UserProvidedInstance_NotDisposedByContainer()
    {
        var svc = new DisposableService();

        await using (var container = new SvcContainer(autoConfigureFromGenerator: false))
        {
            container.RegisterSingle<IDisposableService>(svc);
            container.Build();

            await using var scope = container.CreateScope();
            var resolved = scope.GetService<IDisposableService>();
            await Assert.That(resolved).IsSameReferenceAs(svc);
        }

        // Caller retains ownership — container must NOT dispose user-provided instances.
        await Assert.That(svc.IsDisposed).IsFalse();
    }

    [Test]
    public async Task FromInstance_DescriptorRegistration_NotDisposedByContainer()
    {
        var svc = new DisposableService();

        await using (var container = new SvcContainer(autoConfigureFromGenerator: false))
        {
            container.Register(SvcDescriptor.FromInstance(typeof(IDisposableService), svc));
            container.Build();

            await using var scope = container.CreateScope();
            scope.GetService<IDisposableService>();
        }

        await Assert.That(svc.IsDisposed).IsFalse();
    }

    [Test]
    public async Task FactoryProducedSingleton_StillDisposedByContainer()
    {
        DisposableService? created = null;

        await using (var container = new SvcContainer(autoConfigureFromGenerator: false))
        {
            container.RegisterSingleton<IDisposableService>(_ =>
            {
                created = new DisposableService();
                return created;
            });
            container.Build();

            await using var scope = container.CreateScope();
            scope.GetService<IDisposableService>();
        }

        // Container-created singletons are still owned by the container.
        await Assert.That(created).IsNotNull();
        await Assert.That(created!.IsDisposed).IsTrue();
    }

    // ------------------------------------------------------------------
    // Bug #1 (singleton LIFO disposal):
    //   Singletons must be disposed in reverse construction order so that
    //   a service can safely use its dependencies in its own Dispose method.
    //   The previous implementation iterated the FrozenDictionary in hash
    //   order, which is unrelated to construction order — this could call
    //   Dispose on a dependency BEFORE the dependent that holds it.
    // ------------------------------------------------------------------

    private interface ISingletonA
    {
        string Name { get; }
    }

    private interface ISingletonB
    {
        ISingletonA Dependency { get; }
    }

    private interface ISingletonC
    {
        ISingletonB Dependency { get; }
    }

    private sealed class OrderRecorder
    {
        private readonly List<string> _order = [];
        private readonly object _gate = new();

        public void Record(string name)
        {
            lock (_gate)
                _order.Add(name);
        }

        public IReadOnlyList<string> Order
        {
            get
            {
                lock (_gate)
                    return _order.ToArray();
            }
        }
    }

    private sealed class SingletonA(OrderRecorder rec) : ISingletonA, IDisposable
    {
        public string Name => "A";

        public void Dispose() => rec.Record("A");
    }

    private sealed class SingletonB(OrderRecorder rec, ISingletonA a) : ISingletonB, IDisposable
    {
        public ISingletonA Dependency { get; } = a;

        public void Dispose() => rec.Record("B");
    }

    private sealed class SingletonC(OrderRecorder rec, ISingletonB b) : ISingletonC, IDisposable
    {
        public ISingletonB Dependency { get; } = b;

        public void Dispose() => rec.Record("C");
    }

    [Test]
    public async Task SingletonDisposal_HappensInReverseConstructionOrder()
    {
        var recorder = new OrderRecorder();

        await using (var container = new SvcContainer(autoConfigureFromGenerator: false))
        {
            container.RegisterSingle(recorder);
            container.RegisterSingleton<ISingletonA>(
                s => new SingletonA(s.GetService<OrderRecorder>())
            );
            container.RegisterSingleton<ISingletonB>(
                s => new SingletonB(s.GetService<OrderRecorder>(), s.GetService<ISingletonA>())
            );
            container.RegisterSingleton<ISingletonC>(
                s => new SingletonC(s.GetService<OrderRecorder>(), s.GetService<ISingletonB>())
            );
            container.Build();

            await using var scope = container.CreateScope();
            // Force construction in A → B → C order by resolving C, which
            // transitively constructs A then B then C.
            scope.GetService<ISingletonC>();
        }

        // Expect LIFO: C, B, A (reverse of construction).
        await Assert.That(recorder.Order.Count).IsEqualTo(3);
        await Assert.That(recorder.Order[0]).IsEqualTo("C");
        await Assert.That(recorder.Order[1]).IsEqualTo("B");
        await Assert.That(recorder.Order[2]).IsEqualTo("A");
    }

    // ------------------------------------------------------------------
    // Bug A: scoped disposal must also be LIFO (same structural issue as
    // singleton Bug #1 — DisposeScopedInstancesAsync iterates
    // ConcurrentDictionary.Values in hash order, not construction order).
    // ------------------------------------------------------------------

    private interface IScopedA : IDisposable
    {
        string Name { get; }
    }

    private interface IScopedB : IDisposable
    {
        IScopedA Dependency { get; }
    }

    private interface IScopedC : IDisposable
    {
        IScopedB Dependency { get; }
    }

    private sealed class ScopedA(OrderRecorder rec) : IScopedA, IDisposable
    {
        public string Name => "A";

        public void Dispose() => rec.Record("A");
    }

    private sealed class ScopedB(OrderRecorder rec, IScopedA a) : IScopedB, IDisposable
    {
        public IScopedA Dependency { get; } = a;

        public void Dispose() => rec.Record("B");
    }

    private sealed class ScopedC(OrderRecorder rec, IScopedB b) : IScopedC, IDisposable
    {
        public IScopedB Dependency { get; } = b;

        public void Dispose() => rec.Record("C");
    }

    [Test]
    public async Task ScopedDisposal_HappensInReverseConstructionOrder()
    {
        var recorder = new OrderRecorder();

        await using var container = new SvcContainer(autoConfigureFromGenerator: false);
        container.RegisterSingle(recorder);
        container.RegisterScoped<IScopedA>(s => new ScopedA(s.GetService<OrderRecorder>()));
        container.RegisterScoped<IScopedB>(
            s => new ScopedB(s.GetService<OrderRecorder>(), s.GetService<IScopedA>())
        );
        container.RegisterScoped<IScopedC>(
            s => new ScopedC(s.GetService<OrderRecorder>(), s.GetService<IScopedB>())
        );

        await using var scope = container.CreateScope();
        scope.GetService<IScopedC>(); // cascading: A → B → C

        await scope.DisposeAsync();

        await Assert.That(recorder.Order.Count).IsEqualTo(3);
        await Assert.That(recorder.Order[0]).IsEqualTo("C");
        await Assert.That(recorder.Order[1]).IsEqualTo("B");
        await Assert.That(recorder.Order[2]).IsEqualTo("A");
    }

    // ------------------------------------------------------------------
    // Bug B: hosted singleton's captive scoped dependency must outlive
    // the hosted service's Dispose. DisposeAsync currently runs
    // DisposeHostingScopeAsync BEFORE DisposeSingletonInstancesAsync,
    // so scoped deps are freed while hosted.Dispose() still needs them.
    // ------------------------------------------------------------------

    private sealed class HostedWithScopedDep(OrderRecorder rec, IDisposableService scopedDep)
        : IHostedSvc,
            IDisposable
    {
        public IDisposableService ScopedDep => scopedDep;

        public Task StartAsync(CancellationToken ct) => Task.CompletedTask;

        public Task StopAsync(CancellationToken ct) => Task.CompletedTask;

        public void Dispose()
        {
            // Should not be disposed yet — if it is, the ordering is wrong.
            if (scopedDep.IsDisposed)
                rec.Record("use-after-free");
            else
                rec.Record("ok");
        }
    }

    [Test]
    public async Task HostedSingletonDispose_ScopedDependencyStillAlive()
    {
        var recorder = new OrderRecorder();

        await using var container = new SvcContainer(autoConfigureFromGenerator: false);
        container.RegisterSingle(recorder);
        container.RegisterScoped<IDisposableService>(_ => new DisposableService());
        container.RegisterHostedSvc<HostedWithScopedDep>(
            sp =>
                new HostedWithScopedDep(
                    sp.GetService<OrderRecorder>(),
                    sp.GetService<IDisposableService>()!
                )
        );
        container.Build();

        await new SvcHost(container).StartAsync();
        await container.DisposeAsync();

        await Assert.That(recorder.Order.Count).IsEqualTo(1);
        await Assert.That(recorder.Order[0]).IsEqualTo("ok");
    }
}
