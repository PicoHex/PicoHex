namespace PicoHex.DI.Test;

public class IocContainerTests
{
    private readonly ISvcContainer _container = Bootstrap.CreateContainer();

    [Fact]
    public void Bootstrapping_ShouldResolveSelf()
    {
        // Arrange
        var provider = _container.CreateProvider();

        // Act
        var resolvedContainer = provider.Resolve<ISvcContainer>();
        var resolvedProvider = provider.Resolve<ISvcProvider>();

        // Assert
        Assert.Same(_container, resolvedContainer);
        Assert.Same(provider, resolvedProvider);
    }

    [Fact]
    public void BasicInjection_ShouldResolveDependencies()
    {
        // Arrange
        _container.RegisterTransient<A>();
        _container.RegisterTransient<IB, B>();
        _container.RegisterTransient<IC, C>();
        var provider = _container.CreateProvider();

        // Act
        var result = provider.Resolve<A>();

        // Assert
        Assert.NotNull(result);
        Assert.IsType<B>(result.B);
        Assert.IsType<C>(((B)result.B).C);
    }

    [Fact]
    public void IEnumerableInjection_ShouldResolveAllImplementations()
    {
        // Arrange
        _container.RegisterTransient<IA, A>();
        _container.RegisterTransient<IB, B>();
        _container.RegisterTransient<IC, C>();
        _container.RegisterTransient<IService, A>();
        _container.RegisterTransient<IService, B>();
        _container.RegisterTransient<IService, C>();
        _container.RegisterTransient<D>();
        var provider = _container.CreateProvider();

        // Act
        var result = provider.Resolve<D>();

        // Assert
        Assert.Equal(3, result.Services.Count());
        Assert.Contains(result.Services, s => s is A);
        Assert.Contains(result.Services, s => s is B);
        Assert.Contains(result.Services, s => s is C);
    }

    [Fact]
    public void CircularDependency_ShouldThrowException()
    {
        // Arrange
        _container.RegisterTransient<ICircularA, CircularA>();
        _container.RegisterTransient<ICircularB, CircularB>();
        _container.RegisterTransient<ICircularC, CircularC>();
        var provider = _container.CreateProvider();

        // Act & Assert
        var ex = Assert.Throws<InvalidOperationException>(() => provider.Resolve<ICircularA>());

        Assert.Contains("Circular dependency detected", ex.Message);
    }

    [Fact]
    public void DuplicateRegistration_ShouldThrowException()
    {
        // Arrange
        _container.RegisterTransient<IA, A>();

        // Act & Assert
        var ex = Assert.Throws<InvalidOperationException>(
            () => _container.RegisterTransient<IA, A>()
        );

        Assert.Contains("Duplicate registration for type", ex.Message);
    }

    [Fact]
    public void DuplicateSingletonRegistration_ShouldThrowException()
    {
        // Arrange
        var instance = new C();
        _container.RegisterSingle<IC>(instance);

        // Act & Assert
        var ex = Assert.Throws<InvalidOperationException>(
            () => _container.RegisterSingle<IC>(instance)
        );

        Assert.Contains("Duplicate registration for type", ex.Message);
    }

    [Fact]
    public void AotCompatibility_ShouldResolveTypes()
    {
        // Arrange
        _container.RegisterTransient<A>();
        _container.RegisterTransient<IB, B>();
        _container.RegisterTransient<IC, C>();
        var provider = _container.CreateProvider();

        // Act
        var result = provider.Resolve<A>();

        // Assert
        Assert.NotNull(result);
    }
}

// Test interfaces and classes
public interface IService;

public class ServiceImpl : IService;

public interface IA : IService;

public interface IB : IService;

public interface IC : IService;

public class A(IB b) : IA
{
    public IB B { get; } = b;
}

public class B(IC c) : IB
{
    public IC C { get; } = c;
}

public class C : IC;

public class D(IEnumerable<IService> services)
{
    public IEnumerable<IService> Services { get; } = services;
}

public interface ICircularA;

public interface ICircularB;

public interface ICircularC;

public class CircularA(ICircularB b) : ICircularA;

public class CircularB(ICircularC c) : ICircularB;

public class CircularC(ICircularA a) : ICircularC;
