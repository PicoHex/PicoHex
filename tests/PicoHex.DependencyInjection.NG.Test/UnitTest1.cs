namespace PicoHex.DependencyInjection.NG.Test;

public class ContainerTests
{
    public interface IService;

    public class ServiceA : IService;

    public interface IScopedService;

    public class ScopedServiceImpl : IScopedService;

    public interface IPerThreadService;

    public class PerThreadServiceImpl : IPerThreadService;

    public interface IGenericService<T>;

    public class GenericServiceImpl<T> : IGenericService<T>;

    public interface ICircularA;

    public interface ICircularB;

    public class CircularA : ICircularA
    {
        public CircularA(ICircularB b) { }
    }

    public class CircularB : ICircularB
    {
        public CircularB(ICircularA a) { }
    }

    [Fact]
    public void Should_Detect_Circular_Dependency()
    {
        // Arrange
        var registry = new Registry()
            .Register<ICircularA, CircularA>()
            .Register<ICircularB, CircularB>();

        var container = registry.Build();

        // Act & Assert
        var ex = Assert.Throws<InvalidOperationException>(() => container.GetService<ICircularA>());
        Assert.Contains(
            "Circular dependency detected: ICircularA -> ICircularB -> ICircularA",
            ex.Message
        );
    }

    [Fact]
    public void Singleton_Should_Return_Same_Instance()
    {
        var registry = new Registry().Register<IService, ServiceA>(Lifetime.Singleton);

        var container = registry.Build();
        var instance1 = container.GetService<IService>();
        var instance2 = container.GetService<IService>();

        Assert.Same(instance1, instance2);
    }

    [Fact]
    public void Scoped_Should_Return_Different_Instances_In_Different_Scopes()
    {
        var registry = new Registry().Register<IScopedService, ScopedServiceImpl>(Lifetime.Scoped);

        var container = registry.Build();
        var instance1 = container.CreateScope().GetService<IScopedService>();
        var instance2 = container.CreateScope().GetService<IScopedService>();

        Assert.NotSame(instance1, instance2);
    }

    [Fact]
    public void PerThread_Should_Return_Different_Instances_In_Different_Threads()
    {
        var registry = new Registry().Register<IPerThreadService, PerThreadServiceImpl>(
            Lifetime.PerThread
        );

        var container = registry.Build();
        IPerThreadService? threadInstance = null;

        var thread = new Thread(() =>
        {
            threadInstance = container.GetService<IPerThreadService>();
        });

        thread.Start();
        thread.Join();

        var mainInstance = container.GetService<IPerThreadService>();
        Assert.NotSame(mainInstance, threadInstance);
    }

    [Fact]
    public void Closed_Generic_Should_Resolve_Correctly()
    {
        var registry = new Registry().Register(
            typeof(IGenericService<>),
            typeof(GenericServiceImpl<>),
            Lifetime.Transient
        );

        var container = registry.Build();
        var service = container.GetService<IGenericService<string>>();

        Assert.IsType<GenericServiceImpl<string>>(service);
    }

    [Fact]
    public void Container_Should_Resolve_Itself()
    {
        var registry = new Registry();
        var container = registry.Build();

        var resolved = container.GetService<IContainer>();
        Assert.Same(container, resolved);
    }

    public class DisposableService : IDisposable
    {
        public bool IsDisposed { get; private set; }

        public void Dispose() => IsDisposed = true;
    }

    [Fact]
    public void Singleton_Should_Be_Disposed()
    {
        var registry = new Registry().Register<DisposableService, DisposableService>(
            Lifetime.Singleton
        );

        var container = registry.Build();
        var service = container.GetService<DisposableService>();

        container.Dispose();
        Assert.True(service.IsDisposed);
    }
}
