namespace PicoDI.Test;

public class SingletonRootScopeTests
{
    public interface IScopedDep { }

    public sealed class ScopedDep : IScopedDep, IDisposable
    {
        public static int DisposedCount;
        public bool Disposed;
        public void Dispose()
        {
            Disposed = true;
            DisposedCount++;
        }
    }

    public interface ISingletonWithDep
    {
        IScopedDep Dep { get; }
    }

    public sealed class SingletonWithDep(IScopedDep dep) : ISingletonWithDep
    {
        public IScopedDep Dep { get; } = dep;
    }

    [Test]
    public async Task SingletonFactory_ResolvesScopedDepFromContainerRoot_NotRequestScope()
    {
        var container = new SvcContainer();
        container.RegisterScoped<IScopedDep>(_ => new ScopedDep());
        container.RegisterSingleton<ISingletonWithDep>(scope =>
            new SingletonWithDep(scope.GetService<IScopedDep>()));
        container.Build();

        // Trigger creation from a short-lived scope (the E1 scenario)
        ISingletonWithDep singleton;
        {
            await using var requestScope = container.CreateScope();
            singleton = requestScope.GetService<ISingletonWithDep>();
        } // request scope disposed — scoped dep must NOT die with it

        await Assert.That(singleton).IsNotNull();
        await Assert.That(singleton.Dep).IsNotNull();
        // E1 regression: under the bug the dep was anchored to the disposed
        // request scope and is dead — assert it is still alive.
        await Assert.That(((ScopedDep)singleton.Dep).Disposed).IsFalse();

        await container.DisposeAsync();
    }

    [Test]
    public async Task SingletonFactory_ScopedDep_DisposedAtContainerDisposal_NotBefore()
    {
        ScopedDep.DisposedCount = 0;
        var container = new SvcContainer();
        container.RegisterScoped<IScopedDep>(_ => new ScopedDep());
        container.RegisterSingleton<ISingletonWithDep>(scope =>
            new SingletonWithDep(scope.GetService<IScopedDep>()));
        container.Build();

        await using (var requestScope = container.CreateScope())
        {
            _ = requestScope.GetService<ISingletonWithDep>();
        }

        // The dep must outlive the request scope (lives in the container root).
        await Assert.That(ScopedDep.DisposedCount).IsEqualTo(0);

        await container.DisposeAsync();
        await Assert.That(ScopedDep.DisposedCount).IsEqualTo(1);
    }

    [Test]
    public async Task SingletonFactory_FromChildScope_ReachesContainerRoot()
    {
        var container = new SvcContainer();
        container.RegisterScoped<IScopedDep>(_ => new ScopedDep());
        container.RegisterSingleton<ISingletonWithDep>(scope =>
            new SingletonWithDep(scope.GetService<IScopedDep>()));
        container.Build();

        ISingletonWithDep singleton;
        {
            await using var root = container.CreateScope();
            await using var child = root.CreateScope();
            singleton = child.GetService<ISingletonWithDep>(); // creation from a child scope
        } // child + root disposed — dep must NOT die with them

        await Assert.That(singleton.Dep).IsNotNull();
        await Assert.That(((ScopedDep)singleton.Dep).Disposed).IsFalse();

        await container.DisposeAsync();
    }

    [Test]
    public async Task SingletonFactory_ReceivesContainerRootScope_NotRequestScope()
    {
        SvcScope? seen = null;
        var container = new SvcContainer();
        container.RegisterSingleton<object>(scope =>
        {
            seen = (SvcScope)scope;
            return new object();
        });
        container.Build();

        await using var root = container.CreateScope();
        _ = root.GetService<object>();

        await Assert.That(seen).IsNotNull();
        // The factory must receive the container-internal root scope,
        // NOT the scope that triggered resolution.
        await Assert.That(ReferenceEquals(seen, root)).IsFalse();
    }
}
