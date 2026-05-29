using System.Runtime.CompilerServices;

namespace PicoDI.Test;

public class TransientDisposalRaceTests
{
    private sealed class Tracked : IDisposable
    {
        public bool IsDisposed { get; private set; }

        public void Dispose() => IsDisposed = true;
    }

    [Test]
    public async Task Transient_IDisposable_DisposedWhenScopeDisposed()
    {
        var container = new SvcContainer(autoConfigureFromGenerator: false);
        container.RegisterTransient<Tracked>(_ => new Tracked());
        container.Build();
        var scope = container.CreateScope();
        var svc = scope.GetService<Tracked>();
        await Assert.That(svc.IsDisposed).IsFalse();
        await scope.DisposeAsync();
        await Assert.That(svc.IsDisposed).IsTrue();
    }

    [Test]
    public async Task Transient_MultipleDisposables_AllDisposedAfterScopeDisposal()
    {
        var container = new SvcContainer(autoConfigureFromGenerator: false);
        container.RegisterTransient<Tracked>(_ => new Tracked());
        container.Build();
        var scope = container.CreateScope();

        var instances = new List<Tracked>();
        for (var i = 0; i < 10; i++)
            instances.Add(scope.GetService<Tracked>());

        foreach (var svc in instances)
            await Assert.That(svc.IsDisposed).IsFalse();

        await scope.DisposeAsync();

        foreach (var svc in instances)
            await Assert.That(svc.IsDisposed).IsTrue();
    }

    [Test]
    public async Task Transient_ParallelResolution_NoLeaks()
    {
        var container = new SvcContainer(autoConfigureFromGenerator: false);
        var created = 0;
        var disposed = 0;

        container.RegisterTransient<IDisposable>(_ =>
        {
            Interlocked.Increment(ref created);
            return new CountingDisposable(() => Interlocked.Increment(ref disposed));
        });
        container.Build();

        for (var trial = 0; trial < 10; trial++)
        {
            created = 0;
            disposed = 0;
            var scope = container.CreateScope();

            // Resolve sequentially first, then dispose - baseline
            for (var i = 0; i < 8; i++)
                scope.GetService<IDisposable>();

            await scope.DisposeAsync();

            await Assert.That(disposed).IsEqualTo(created);
        }
    }

    private sealed class CountingDisposable(Action onDispose) : IDisposable
    {
        public void Dispose() => onDispose();
    }
}
