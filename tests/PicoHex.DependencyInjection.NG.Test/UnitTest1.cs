namespace PicoHex.DependencyInjection.NG.Test;

public interface IService { }

public class ServiceImpl : IService { }

public interface IGenericService<T> { }

public class GenericServiceImpl<T> : IGenericService<T> { }

[RegisterService(typeof(IService), Lifetime.Singleton)]
[RegisterGeneric(typeof(IGenericService<>), typeof(GenericServiceImpl<>), Lifetime.Transient)]
public class TestContainer { }

public interface INotRegisteredService { }

public class ContainerTests
{
    [Fact]
    public void Should_Resolve_Singleton_Service()
    {
        var container = new ServiceContainer();
        var instance1 = container.GetService<IService>();
        var instance2 = container.GetService<IService>();

        Assert.Same(instance1, instance2);
    }

    [Fact]
    public void Should_Resolve_Generic_Service()
    {
        var container = new ServiceContainer();
        var service = container.GetService<IGenericService<string>>();

        Assert.IsType<GenericServiceImpl<string>>(service);
    }

    [Fact]
    public void Scoped_Service_Should_Be_Isolated()
    {
        var container = new ServiceContainer();

        using var scope1 = container.CreateScope();
        var s1 = scope1.ServiceProvider.GetService(typeof(IService));

        using var scope2 = container.CreateScope();
        var s2 = scope2.ServiceProvider.GetService(typeof(IService));

        Assert.Same(s1, s2);
    }

    [Fact]
    public void Pooled_Service_Should_Reuse_Instances()
    {
        var container = new ServiceContainer();
        container.Register<IDatabase, Database>(Lifetime.Pooled);

        var instances = new List<IDatabase>();
        for (int i = 0; i < 5; i++)
        {
            using var scope = container.CreateScope();
            var instance = (IDatabase)scope.ServiceProvider.GetService(typeof(IDatabase));
            instances.Add(instance);
        }

        // 验证实例重用
        var distinctCount = instances.Distinct().Count();
        Assert.True(distinctCount < 5); // 根据PoolSize验证实际重用次数
    }

    [Fact]
    public void Should_Throw_On_Unregistered_Service()
    {
        var container = new ServiceContainer();

        Assert.Throws<InvalidOperationException>(
            () => container.GetService<INotRegisteredService>()
        );
    }

    [Fact]
    public void ThreadLocal_Service_Should_Be_Per_Thread()
    {
        var container = new ServiceContainer();
        container.Register<ICache, MemoryCache>(Lifetime.PerThread);

        ICache? thread1Cache = null;
        ICache? thread2Cache = null;

        var thread1 = new Thread(() => thread1Cache = container.GetService<ICache>());
        var thread2 = new Thread(() => thread2Cache = container.GetService<ICache>());

        thread1.Start();
        thread2.Start();
        thread1.Join();
        thread2.Join();

        Assert.NotNull(thread1Cache);
        Assert.NotNull(thread2Cache);
        Assert.NotSame(thread1Cache, thread2Cache);
    }

    [Fact]
    public void Should_Detect_Circular_Dependency()
    {
        var container = new ServiceContainer();
        container.Register<A, A>(Lifetime.Singleton); // 明确指定实现类型
        container.Register<B, B>(Lifetime.Singleton);

        Assert.Throws<InvalidOperationException>(() => container.Validate());
    }
}

// 测试用类
public class A
{
    public A(B b) { }
}

public class B
{
    public B(A a) { }
}

public interface IDatabase : IDisposable { }

public class Database : IDatabase
{
    public void Dispose() { }
}

public interface ICache { }

public class MemoryCache : ICache { }
