namespace Pico.IoC.Test;

public class EnhancedLifecycleTests : IDisposable
{
    private readonly ISvcContainer _container;
    private readonly ISvcProvider _provider;

    public EnhancedLifecycleTests()
    {
        _container = Bootstrap.CreateContainer();
        _provider = _container.GetProvider();
    }

    public void Dispose() => (_container as IDisposable)?.Dispose();

    #region Singleton 增强测试
    [Fact]
    public void Singleton_FactoryRegistration_ShouldInvokeFactoryOnce()
    {
        // Arrange
        var callCount = 0;
        _container.RegisterSingle<IService>(_ =>
        {
            callCount++;
            return new ServiceImpl();
        });

        // Act
        _provider.Resolve<IService>();
        _provider.Resolve<IService>();

        // Assert
        Assert.Equal(1, callCount);
    }

    [Fact]
    public void Singleton_InstanceRegistration_ShouldAlwaysReturnSameInstance()
    {
        // Arrange
        var instance = new ServiceImpl();
        _container.RegisterSingle<IService>(instance);

        // Act & Assert
        Assert.Same(instance, _provider.Resolve<IService>());
        Assert.Same(instance, _provider.Resolve<IService>());
    }

    [Fact]
    public void Singleton_OpenGeneric_ShouldResolveSameInstance()
    {
        // Arrange
        _container.RegisterSingle(typeof(IRepository<>), typeof(Repository<>));

        // Act
        var repo1 = _provider.Resolve<IRepository<int>>();
        var repo2 = _provider.Resolve<IRepository<string>>();
        var repo3 = _provider.Resolve<IRepository<int>>();

        // Assert
        Assert.Same(repo1, repo3);
        Assert.NotSame(repo1, repo2);
    }
    #endregion

    #region Scoped 增强测试
    [Fact]
    public void Scoped_FactoryRegistration_ShouldCreatePerScope()
    {
        // Arrange
        var sequence = 0;
        _container.RegisterScoped<IService>(_ => new ServiceImpl { Id = ++sequence });

        // Act
        int id1,
            id2;
        using (var scope1 = _provider.CreateScope())
        {
            id1 = ((ServiceImpl)scope1.Resolve<IService>()).Id;
        }

        using (var scope2 = _provider.CreateScope())
        {
            id2 = ((ServiceImpl)scope2.Resolve<IService>()).Id;
        }

        // Assert
        Assert.Equal(1, id1);
        Assert.Equal(2, id2);
    }

    [Fact]
    public void Scoped_WithSingletonDependency_ShouldResolveCorrectInstances()
    {
        // Arrange
        _container.RegisterSingle<IServiceA, ServiceAImpl>();
        _container.RegisterScoped<IServiceB, ServiceBImpl>();
        _container.RegisterSingle<IService, ServiceImpl>();

        // Act
        IServiceA singleton1,
            singleton2;
        IServiceB scoped1,
            scoped2;

        using (var scope = _provider.CreateScope())
        {
            singleton1 = scope.Resolve<IServiceA>();
            scoped1 = scope.Resolve<IServiceB>();
        }

        using (var scope = _provider.CreateScope())
        {
            singleton2 = scope.Resolve<IServiceA>();
            scoped2 = scope.Resolve<IServiceB>();
        }

        // Assert
        Assert.Same(singleton1, singleton2);
        Assert.NotSame(scoped1, scoped2);
    }
    #endregion

    #region Transient 增强测试
    [Fact]
    public void Transient_FactoryRegistration_ShouldCreateNewInstanceEachTime()
    {
        // Arrange
        var counter = 0;
        _container.RegisterTransient<IService>(_ => new ServiceImpl { Id = ++counter });

        // Act
        var instance1 = (ServiceImpl)_provider.Resolve<IService>();
        var instance2 = (ServiceImpl)_provider.Resolve<IService>();

        // Assert
        Assert.Equal(1, instance1.Id);
        Assert.Equal(2, instance2.Id);
    }

    [Fact]
    public void Transient_WithScopedDependency_ShouldCreateNewDependencies()
    {
        // Arrange
        _container.RegisterScoped<IServiceA, ServiceAImpl>();
        _container.RegisterScoped<IServiceB, ServiceBImpl>();
        _container.RegisterSingle<IService, ServiceImpl>();

        // Act
        IServiceB transient1,
            transient2;
        using (var scope = _provider.CreateScope())
        {
            transient1 = scope.Resolve<IServiceB>();
            transient2 = scope.Resolve<IServiceB>();
        }

        // Assert
        Assert.Same(transient1, transient2);
        Assert.Same(((ServiceBImpl)transient1).ServiceA, ((ServiceBImpl)transient2).ServiceA); // Scoped 依赖相同
    }
    #endregion

    #region 生命周期覆盖测试
    [Fact]
    public void LastRegistration_ShouldOverridePreviousLifecycle()
    {
        // Arrange
        _container.RegisterTransient<IService, ServiceImpl>();
        _container.RegisterSingle<IService, ServiceImpl>();

        // Act
        var instance1 = _provider.Resolve<IService>();
        var instance2 = _provider.Resolve<IService>();

        // Assert
        Assert.Same(instance1, instance2);
    }
    #endregion

    #region 测试类型
    public interface IRepository<T>;

    public class Repository<T> : IRepository<T>;

    public class ServiceImpl : IService
    {
        public int Id { get; set; }
    }

    public class ServiceBImpl(IServiceA serviceA) : IServiceB
    {
        public IServiceA ServiceA { get; } = serviceA;
    }
    #endregion
}
