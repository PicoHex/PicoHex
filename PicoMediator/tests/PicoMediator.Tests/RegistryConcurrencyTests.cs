namespace PicoMediator.Tests;

// Regression for the loader-lock deadlock (2026-08-05): TryApplyConfiguration
// must NOT invoke configurators while holding ApplyLock. A configurator's
// first JIT of a cold assembly can require the loader lock; if another thread
// is inside that assembly's module initializer (which calls Register), the
// two threads deadlock (ApplyLock <-> loader lock). These tests pin the
// observable contract that keeps the registry deadlock-free:
//   1. Register is never blocked by an in-flight configurator
//   2. configurators may safely call Register (module-initializer pattern)
//   3. concurrent TryApplyConfiguration on MANY containers applies exactly once
//   4. a throwing configurator rolls back the applied marker (retry allowed)
//
// All test configurators use dedicated keys/service types so they persist in
// the global registry without interfering with the real generated ones.
public class RegistryConcurrencyTests
{
    // Instance counters captured by the persisted configurators' closures:
    // TUnit runs tests in parallel, so each test must own its counting state.
    private sealed class Counter
    {
        public int Value;
    }

    // Static (process-lifetime) gates for the slow-configurator test: the
    // configurator persists in the global registry, so it must never reference
    // test-local objects (disposed after the test).
    private static readonly ManualResetEventSlim SlowConfiguratorStarted = new();
    private static readonly ManualResetEventSlim SlowConfiguratorRelease = new();

    public interface IProbeBase : IEvent { }

    public record ProbeEvent(int Id) : IProbeBase;

    public sealed class NoopSubscriber : ISubscriber<ProbeEvent>
    {
        public ValueTask Handle(ProbeEvent e, CancellationToken ct) => ValueTask.CompletedTask;
    }

    [Test]
    public async Task Register_IsNeverBlocked_ByInFlightConfigurator()
    {
        // A slow configurator holds the application in progress; Register must
        // complete immediately (old code: blocked on ApplyLock until the
        // configurator finished -> this test failed).
        SlowConfiguratorStarted.Reset();
        SlowConfiguratorRelease.Reset();

        var container = new SvcContainer(autoConfigureFromGenerator: false);

        // TUnit runs test classes in parallel: the persisted configurator would
        // block OTHER tests' AddPicoMediator too — gate it by container identity.
        MediatorAutoSubscriptionRegistry.Register(
            "test::slow-configurator",
            c =>
            {
                if (!ReferenceEquals(c, container))
                    return;
                SlowConfiguratorStarted.Set();
                SlowConfiguratorRelease.Wait(TimeSpan.FromSeconds(30));
            }
        );

        var applyTask = Task.Run(() =>
            MediatorAutoSubscriptionRegistry.TryApplyConfiguration(container)
        );

        await Assert.That(SlowConfiguratorStarted.Wait(TimeSpan.FromSeconds(10))).IsTrue();

        var registerTask = Task.Run(() =>
            MediatorAutoSubscriptionRegistry.Register("test::fast-register", _ => { })
        );

        try
        {
            // Give Register ample time; it must NOT wait for the configurator.
            await Task.Delay(1500);
            await Assert.That(registerTask.IsCompleted).IsTrue();
        }
        finally
        {
            // Always release the gate — even on assertion failure — so the
            // in-flight configurator finishes and later tests never wait 30s.
            SlowConfiguratorRelease.Set();
        }

        await applyTask;
    }

    [Test]
    public async Task Configurator_MayCallRegister_InsideItself()
    {
        // Simulates a module initializer running while a configurator executes
        // (the .cctor -> Register path from the deadlock) — must not throw
        // (Lock re-entrancy) and the replacement must apply to fresh containers.
        MediatorAutoSubscriptionRegistry.Register(
            "test::inner-configurator",
            c =>
            {
                MediatorAutoSubscriptionRegistry.Register(
                    "test::inner-replacement",
                    c2 =>
                        c2.Register(
                            SvcDescriptor.Create(
                                typeof(ISubscriber<ProbeEvent>),
                                static _ => new NoopSubscriber(),
                                SvcLifetime.Transient
                            )
                        )
                );
            }
        );

        var fresh = new SvcContainer(autoConfigureFromGenerator: false);
        fresh.AddPicoMediator();
        fresh.Build();
        await using var freshScope = fresh.CreateScope();
        var subscribers = freshScope.GetServices<ISubscriber<ProbeEvent>>();

        await Assert.That(subscribers).IsNotEmpty();
    }

    [Test]
    public async Task ConcurrentTryApply_OnManyContainers_AppliesExactlyOncePerContainer()
    {
        var counter = new Counter();

        const int containerCount = 64;
        const int threadsPerContainer = 4;
        var containers = new SvcContainer[containerCount];
        for (var i = 0; i < containerCount; i++)
            containers[i] = new SvcContainer(autoConfigureFromGenerator: false);

        // Count only THIS test's containers: TUnit runs classes in parallel,
        // and the persisted configurator would also run for other tests'
        // AddPicoMediator calls.
        var mine = new HashSet<SvcContainer>(containers);
        MediatorAutoSubscriptionRegistry.Register(
            "test::counting-configurator",
            c =>
            {
                if (mine.Contains(c))
                    Interlocked.Increment(ref counter.Value);
            }
        );

        var tasks = new List<Task>();
        foreach (var c in containers)
        {
            for (var t = 0; t < threadsPerContainer; t++)
            {
                tasks.Add(
                    Task.Run(() => MediatorAutoSubscriptionRegistry.TryApplyConfiguration(c))
                );
            }
        }

        await Task.WhenAll(tasks);

        await Assert.That(counter.Value).IsEqualTo(containerCount);
    }

    [Test]
    public async Task ThrowingConfigurator_RollsBackAppliedMarker_AllowsRetry()
    {
        var counter = new Counter();

        var container = new SvcContainer(autoConfigureFromGenerator: false);

        // Gate by container identity: other parallel test classes also trigger
        // this persisted configurator via their AddPicoMediator calls.
        MediatorAutoSubscriptionRegistry.Register(
            "test::flaky-configurator",
            c =>
            {
                if (!ReferenceEquals(c, container))
                    return;
                if (Interlocked.Increment(ref counter.Value) is 1)
                    throw new InvalidOperationException("boom");
            }
        );

        await Assert.ThrowsAsync(async () =>
            MediatorAutoSubscriptionRegistry.TryApplyConfiguration(container)
        );

        // Retry after rollback must succeed.
        var ok = MediatorAutoSubscriptionRegistry.TryApplyConfiguration(container);
        await Assert.That(ok).IsTrue();
        await Assert.That(counter.Value).IsEqualTo(2);
    }
}
