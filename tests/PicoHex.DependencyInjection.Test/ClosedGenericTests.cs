namespace PicoHex.DependencyInjection.Test;

public class ClosedGenericTests
{
    [Fact]
    public void Resolve_ClosedGenericType_WhenOpenGenericIsRegistered()
    {
        // 初始化容器
        var registry = new SvcRegistry(new SvcProviderFactory(new SvcScopeFactory()));
        registry.AddServiceDescriptor(
            new SvcDescriptor(typeof(IRepository<>), typeof(Repository<>), SvcLifetime.Transient)
        );
        var provider = registry.CreateProvider();

        // 解析闭合泛型实例
        var userRepo = provider.Resolve(typeof(IRepository<User>));
        var orderRepo = provider.Resolve(typeof(IRepository<Order>));

        // 验证类型
        Assert.IsType<Repository<User>>(userRepo);
        Assert.IsType<Repository<Order>>(orderRepo);
        Assert.NotSame(userRepo, orderRepo); // 瞬态生命周期验证
    }

    [Fact]
    public void Singleton_ClosedGeneric_ReturnsSameInstance()
    {
        var registry = new SvcRegistry(new SvcProviderFactory(new SvcScopeFactory()));
        registry.AddServiceDescriptor(
            new SvcDescriptor(typeof(IRepository<>), typeof(Repository<>), SvcLifetime.Singleton)
        );
        var provider = registry.CreateProvider();

        var instance1 = provider.Resolve(typeof(IRepository<User>));
        var instance2 = provider.Resolve(typeof(IRepository<User>));

        Assert.Same(instance1, instance2); // 单例验证
    }

    [Fact]
    public void Resolve_GenericServiceWithDependencies()
    {
        var registry = new SvcRegistry(new SvcProviderFactory(new SvcScopeFactory()));

        // 注册依赖项
        registry.AddServiceDescriptor(
            new SvcDescriptor(typeof(IRepository<>), typeof(Repository<>), SvcLifetime.Transient)
        );

        // 注册主服务（依赖IRepository<T>）
        registry.AddServiceDescriptor(
            new SvcDescriptor(typeof(IService<>), typeof(Service<>), SvcLifetime.Transient)
        );

        var provider = registry.CreateProvider();
        var service = provider.Resolve(typeof(IService<User>)) as Service<User>;

        Assert.NotNull(service);
        Assert.IsType<Repository<User>>(service.Repository); // 依赖注入验证
    }

    [Fact]
    public void Resolve_UnregisteredClosedGeneric_ThrowsException()
    {
        var registry = new SvcRegistry(new SvcProviderFactory(new SvcScopeFactory()));
        var provider = registry.CreateProvider();

        var ex = Assert.Throws<InvalidOperationException>(
            () => provider.Resolve(typeof(IRepository<string>))
        );

        Assert.Contains("未注册的服务类型", ex.Message);
    }

    public class A<T>
    {
        public A(B<T> b) { }
    }

    public class B<T>
    {
        public B(A<T> a) { }
    }

    [Fact]
    public void Resolve_CircularDependencyInGenerics_ThrowsException()
    {
        var registry = new SvcRegistry(new SvcProviderFactory(new SvcScopeFactory()));
        registry.AddServiceDescriptor(
            new SvcDescriptor(typeof(A<>), typeof(A<>), SvcLifetime.Transient)
        );
        registry.AddServiceDescriptor(
            new SvcDescriptor(typeof(B<>), typeof(B<>), SvcLifetime.Transient)
        );

        var provider = registry.CreateProvider();

        var ex = Assert.Throws<InvalidOperationException>(() => provider.Resolve(typeof(A<int>)));

        Assert.Contains("Circular dependency detected", ex.Message);
    }

    [Fact]
    public void Scoped_ClosedGeneric_IsolatedPerScope()
    {
        var registry = new SvcRegistry(new SvcProviderFactory(new SvcScopeFactory()));
        registry.AddServiceDescriptor(
            new SvcDescriptor(typeof(IRepository<>), typeof(Repository<>), SvcLifetime.Scoped)
        );

        var provider = registry.CreateProvider();
        using var scope1 = provider.CreateScope();
        using var scope2 = provider.CreateScope();

        var instance1 = scope1.Resolve(typeof(IRepository<User>));
        var instance2 = scope1.Resolve(typeof(IRepository<User>));
        var instance3 = scope2.Resolve(typeof(IRepository<User>));

        Assert.Same(instance1, instance2); // 同一作用域内相同
        Assert.NotSame(instance1, instance3); // 不同作用域实例不同
    }
}

public interface IRepository<T> { }

public class Repository<T> : IRepository<T> { }

public interface IService<T> { }

public class Service<T>(IRepository<T> repository) : IService<T>
{
    public IRepository<T> Repository { get; } = repository;
}

public class User { }

public class Order { }
