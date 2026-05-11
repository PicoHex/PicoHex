namespace PicoDI.Test;

/// <summary>
/// Tests for hosted service boundary conditions and error paths.
/// </summary>
public class HostingEdgeCaseTests
{
    #region Test Helpers

    private sealed class TestHostedSvc : IHostedSvc
    {
        public bool Started { get; private set; }
        public bool Stopped { get; private set; }

        public Task StartAsync(CancellationToken ct)
        {
            Started = true;
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken ct)
        {
            Stopped = true;
            return Task.CompletedTask;
        }
    }

    private sealed class SlowStopSvc : IHostedSvc
    {
        public Task StartAsync(CancellationToken ct) => Task.CompletedTask;

        public async Task StopAsync(CancellationToken ct)
        {
            await Task.Delay(10_000, ct);
        }
    }

    private sealed class NotHostedService { }

    private sealed class AnyHostedSvc : IHostedSvc
    {
        public Task StartAsync(CancellationToken ct) => Task.CompletedTask;

        public Task StopAsync(CancellationToken ct) => Task.CompletedTask;
    }

    private sealed class WrapperHostedSvc(TestHostedSvc inner) : IHostedSvc
    {
        public TestHostedSvc Inner => inner;

        public Task StartAsync(CancellationToken ct) => inner.StartAsync(ct);

        public Task StopAsync(CancellationToken ct) => inner.StopAsync(ct);
    }

    #endregion

    #region Negative Tests

    [Test]
    public async Task RegisterHostedSvc_NonIHostedSvcType_ThrowsException()
    {
        await using var container = new SvcContainer(autoConfigureFromGenerator: false);

        await Assert
            .That(() => container.RegisterHostedSvc(typeof(NotHostedService)))
            .Throws<HostedSvcRegistrationException>();
    }

    [Test]
    public async Task Build_NonSingletonHostedSvc_ThrowsException()
    {
        await using var container = new SvcContainer(autoConfigureFromGenerator: false);
        container.Register(
            new SvcDescriptor(typeof(AnyHostedSvc), typeof(AnyHostedSvc), SvcLifetime.Scoped)
        );

        await Assert.That(() => container.Build()).Throws<HostedSvcRegistrationException>();
    }

    #endregion

    #region Empty Container Tests

    [Test]
    public async Task EmptyContainer_StartStop_NoException()
    {
        await using var container = new SvcContainer(autoConfigureFromGenerator: false);
        var host = new SvcHost(container);

        await host.StartAsync();
        await host.StopAsync();
    }

    #endregion

    #region Idempotency Tests

    [Test]
    public async Task StartAsync_MultipleCalls_IsIdempotent()
    {
        var svc = new TestHostedSvc();
        await using var container = new SvcContainer(autoConfigureFromGenerator: false);
        container.RegisterHostedSvc<TestHostedSvc>(_ => svc);
        container.Build();

        var host = new SvcHost(container);
        await host.StartAsync();
        await host.StartAsync();
        await host.StartAsync();

        await Assert.That(svc.Started).IsTrue();
    }

    [Test]
    public async Task StopAsync_BeforeStart_NoException()
    {
        await using var container = new SvcContainer(autoConfigureFromGenerator: false);
        container.RegisterHostedSvc<TestHostedSvc>(_ => new TestHostedSvc());
        container.Build();

        await new SvcHost(container).StopAsync();
    }

    #endregion

    #region Disposal Tests

    [Test]
    public async Task Dispose_CallsStopThenDispose()
    {
        var svc = new TestHostedSvc();
        var container = new SvcContainer(autoConfigureFromGenerator: false);
        container.RegisterHostedSvc<TestHostedSvc>(_ => svc);
        container.Build();

        await new SvcHost(container).StartAsync();
        await Assert.That(svc.Started).IsTrue();
        await Assert.That(svc.Stopped).IsFalse();

        await container.DisposeAsync();

        await Assert.That(svc.Stopped).IsTrue();
    }

    [Test]
    public async Task LimitStopTimeout_ContinuesDisposal()
    {
        await using var container = new SvcContainer(autoConfigureFromGenerator: false);
        container.RegisterHostedSvc<SlowStopSvc>(_ => new SlowStopSvc());
        container.Build();

        await new SvcHost(container).StartAsync();

        // DisposeAsync should complete even with a slow-stopping service
        // (the 30-second async stop timeout will cancel it)
        await container.DisposeAsync();
    }

    #endregion

    #region Concurrency Tests

    [Test]
    public async Task ConcurrentStartStop_NoCrash()
    {
        await using var container = new SvcContainer(autoConfigureFromGenerator: false);
        container.RegisterHostedSvc<TestHostedSvc>(_ => new TestHostedSvc());
        container.Build();

        var host = new SvcHost(container);
        var t1 = host.StartAsync();
        var t2 = host.StopAsync();
        await Task.WhenAll(t1, t2);
    }

    private sealed class OverlapDetectingSvc : IHostedSvc
    {
        private int _inStart;
        private int _inStop;
        public bool OverlapDetected;

        public async Task StartAsync(CancellationToken ct)
        {
            Interlocked.Increment(ref _inStart);
            if (Volatile.Read(ref _inStop) > 0)
                OverlapDetected = true;
            await Task.Delay(30, ct);
            Interlocked.Decrement(ref _inStart);
        }

        public async Task StopAsync(CancellationToken ct)
        {
            Interlocked.Increment(ref _inStop);
            if (Volatile.Read(ref _inStart) > 0)
                OverlapDetected = true;
            await Task.Delay(30, ct);
            Interlocked.Decrement(ref _inStop);
        }
    }

    [Test]
    public async Task ConcurrentStartAndStop_MustNotOverlap()
    {
        var svc = new OverlapDetectingSvc();
        await using var container = new SvcContainer(autoConfigureFromGenerator: false);
        container.RegisterHostedSvc<OverlapDetectingSvc>(_ => svc);
        container.Build();

        var host = new SvcHost(container);

        // Launch Start then immediately Stop to trigger the race.
        var startTask = host.StartAsync();
        await Task.Yield();
        var stopTask = host.StopAsync();
        await Task.WhenAll(startTask, stopTask);

        await Assert.That(svc.OverlapDetected).IsFalse();
    }

    [Test]
    public async Task StopWinsLockRace_StartDoesNotStartOrphanedServices()
    {
        for (int i = 0; i < 500; i++)
        {
            var svc = new TestHostedSvc();
            await using var container = new SvcContainer(autoConfigureFromGenerator: false);
            container.RegisterHostedSvc<TestHostedSvc>(_ => svc);
            container.Build();
            var host = new SvcHost(container);

            var startTask = Task.Run(() => host.StartAsync());
            var stopTask = Task.Run(() => host.StopAsync());
            await Task.WhenAll(startTask, stopTask);

            // Bug 13: if Stop wins the lock race (acquires _hostingLock before
            // Start), StopCore finds no instances and does nothing. Start then
            // starts services — but state is already 2, so no future Stop runs.
            if (svc.Started)
                await Assert.That(svc.Stopped).IsTrue();
        }
    }

    #endregion

    #region Nested Hosted Service Tests

    [Test]
    public async Task NestedHostedService_Injection()
    {
        var inner = new TestHostedSvc();
        await using var container = new SvcContainer(autoConfigureFromGenerator: false);
        container.RegisterHostedSvc<TestHostedSvc>(_ => inner);
        container.RegisterHostedSvc<WrapperHostedSvc>(
            scope => new WrapperHostedSvc(scope.GetService<TestHostedSvc>()!)
        );
        container.Build();

        await new SvcHost(container).StartAsync();

        await Assert.That(inner.Started).IsTrue();
    }

    #endregion
}
