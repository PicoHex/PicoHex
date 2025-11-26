namespace Pico.DI.Test;

public class AotContainerTests : IDisposable
{
    private readonly ISvcContainer _container;
    private readonly ISvcProvider _provider;

    public AotContainerTests()
    {
        _container = AotBootstrap.CreateAotContainer();
        _provider = _container.GetProvider();
    }

    public void Dispose()
    {
        (_container as IDisposable)?.Dispose();
    }

    [Fact]
    public void AotContainer_ShouldCreateSuccessfully()
    {
        // Arrange & Act
        var container = AotBootstrap.CreateAotContainer();
        var provider = container.GetProvider();

        // Assert
        Assert.NotNull(container);
        Assert.NotNull(provider);
        Assert.IsType<AotSvcContainer>(container);
        Assert.IsType<AotSvcProvider>(provider);
    }

    [Fact]
    public void AotContainer_ShouldResolveTransientServices()
    {
        // Arrange
        _container.RegisterTransient<IService, ServiceImpl>();

        // Act
        var instance1 = _provider.Resolve<IService>();
        var instance2 = _provider.Resolve<IService>();

        // Assert
        Assert.NotNull(instance1);
        Assert.NotNull(instance2);
        Assert.NotSame(instance1, instance2);
        Assert.IsType<ServiceImpl>(instance1);
    }

    [Fact]
    public void AotContainer_ShouldResolveSingletonServices()
    {
        // Arrange
        _container.RegisterSingle<IService, ServiceImpl>();

        // Act
        var instance1 = _provider.Resolve<IService>();
        var instance2 = _provider.Resolve<IService>();

        // Assert
        Assert.NotNull(instance1);
        Assert.NotNull(instance2);
        Assert.Same(instance1, instance2);
    }

    [Fact]
    public void AotContainer_ShouldResolveServicesWithDependencies()
    {
        // Arrange
        _container.RegisterTransient<IService, ServiceImpl>();
        _container.RegisterTransient<IServiceA, ServiceAImpl>();

        // Act
        var serviceA = _provider.Resolve<IServiceA>();

        // Assert
        Assert.NotNull(serviceA);
        Assert.IsType<ServiceAImpl>(serviceA);
    }

    [Fact]
    public void AotContainer_ShouldHandleCircularDependencies()
    {
        // Arrange
        _container.RegisterTransient<ICircularA, CircularA>();
        _container.RegisterTransient<ICircularB, CircularB>();
        _container.RegisterTransient<ICircularC, CircularC>();

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => _provider.Resolve<ICircularA>());
    }

    [Fact]
    public void AotContainer_ShouldSupportScopedServices()
    {
        // Arrange
        _container.RegisterScoped<IService, ServiceImpl>();

        // Act
        IService scope1Instance1, scope1Instance2, scope2Instance;

        using (var scope1 = _provider.CreateScope())
        {
            scope1Instance1 = scope1.Resolve<IService>();
            scope1Instance2 = scope1.Resolve<IService>();
        }

        using (var scope2 = _provider.CreateScope())
        {
            scope2Instance = scope2.Resolve<IService>();
        }

        // Assert
        Assert.Same(scope1Instance1, scope1Instance2);
        Assert.NotSame(scope1Instance1, scope2Instance);
    }

    [Fact]
    public void AotContainer_ShouldDisposeCorrectly()
    {
        // Arrange
        _container.RegisterSingle<IDisposableService, DisposableService>();
        var service = _provider.Resolve<IDisposableService>();

        // Act
        _provider.Dispose();

        // Assert
        Assert.True(((DisposableService)service).IsDisposed);
    }

    [Fact]
    public async Task AotContainer_ShouldDisposeAsyncCorrectly()
    {
        // Arrange
        _container.RegisterSingle<IAsyncDisposableService, AsyncDisposableService>();
        var service = _provider.Resolve<IAsyncDisposableService>();

        // Act
        await _provider.DisposeAsync();

        // Assert
        Assert.True(((AsyncDisposableService)service).IsDisposed);
    }

    [Fact]
    public void OptimizedContainer_ShouldCreateCorrectType()
    {
        // Act
        var container = AotBootstrap.CreateOptimizedContainer();

        // Assert
        Assert.NotNull(container);
#if NET10_0_OR_GREATER
        Assert.IsType<AotSvcContainer>(container);
#else
        // Should fallback to regular container
        Assert.IsType<SvcContainer>(container);
#endif
    }
}

#region Test Services

public interface IDisposableService : IDisposable
{
    bool IsDisposed { get; }
}

public class DisposableService : IDisposableService
{
    public bool IsDisposed { get; private set; }

    public void Dispose()
    {
        IsDisposed = true;
    }
}

public interface IAsyncDisposableService : IAsyncDisposable
{
    bool IsDisposed { get; }
}

public class AsyncDisposableService : IAsyncDisposableService
{
    public bool IsDisposed { get; private set; }

    public ValueTask DisposeAsync()
    {
        IsDisposed = true;
        return ValueTask.CompletedTask;
    }
}

#endregion