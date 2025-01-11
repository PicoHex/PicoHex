namespace TestProject1;

public class DiContainerTests
{
    private readonly ISvcRegistry _svcRegistry;
    private readonly ISvcProvider _svcProvider;

    public DiContainerTests()
    {
        _svcRegistry = ContainerBootstrap.CreateRegistry();
        _svcProvider = _svcRegistry.CreateProvider();
    }

    [Fact]
    public void Register_And_Resolve_Transient()
    {
        _svcRegistry.AddService<IService, ServiceImpl>(SvcLifetime.Transient);
        var s1 = _svcProvider.Resolve<IService>();
        var s2 = _svcProvider.Resolve<IService>();

        Assert.NotNull(s1);
        Assert.NotNull(s2);
        Assert.NotSame(s1, s2);
    }

    [Fact]
    public void Register_And_Resolve_TransientA()
    {
        _svcRegistry.AddService<IService, ServiceImpl>(SvcLifetime.Transient);
        _svcRegistry.AddService<IServiceA, ServiceAImpl>(SvcLifetime.Transient);
        _svcRegistry.AddService<IServiceB, ServiceBImpl>(SvcLifetime.Transient);
        var s1 = _svcProvider.Resolve<IServiceA>();
        var s2 = _svcProvider.Resolve<IServiceB>();

        Assert.NotNull(s1);
        Assert.NotNull(s2);
        Assert.NotSame(s1, s2);
    }

    [Fact]
    public void Register_And_Resolve_Singleton()
    {
        _svcRegistry.AddService<IService, ServiceImpl>(SvcLifetime.Singleton);
        var s1 = _svcProvider.Resolve<IService>();
        var s2 = _svcProvider.Resolve<IService>();

        Assert.Same(s1, s2);
    }

    [Fact]
    public void Register_And_Resolve_Scoped()
    {
        _svcRegistry.AddService<IService, ServiceImpl>(SvcLifetime.Scoped);
        IService s1;
        using (var scope1 = _svcProvider.CreateScope())
        {
            s1 = scope1.Resolve<IService>();
            var s2 = scope1.Resolve<IService>();

            Assert.Same(s1, s2);
        }

        using (var scope2 = _svcProvider.CreateScope())
        {
            var s3 = scope2.Resolve<IService>();
            var s4 = scope2.Resolve<IService>();

            Assert.Same(s3, s4);
            Assert.NotSame(s3, s1);
        }
    }

    [Fact]
    public void Register_And_Resolve_PerThread()
    {
        _svcRegistry.AddService<IService, ServiceImpl>(SvcLifetime.PerThread);

        IService? s1 = null;
        IService? s2 = null;

        var t1 = new Thread(() =>
        {
            s1 = _svcProvider.Resolve<IService>();
        });
        var t2 = new Thread(() =>
        {
            s2 = _svcProvider.Resolve<IService>();
        });

        t1.Start();
        t2.Start();
        t1.Join();
        t2.Join();

        Assert.NotNull(s1);
        Assert.NotNull(s2);
        Assert.NotSame(s1, s2);
    }

    [Fact]
    public void Throws_When_Not_Registered()
    {
        Assert.Throws<InvalidOperationException>(() => _svcProvider.Resolve<IService>());
    }
}

public interface IService { }

public class ServiceImpl : IService { }

public interface IServiceA { }

public class ServiceAImpl(IService service) : IServiceA { }

public interface IServiceB { }

public class ServiceBImpl(IService service) : IServiceB { }

public interface INode1 { }

public interface INode2 { }

public class Node1 : INode1
{
    public Node1(INode2 node2) { }
}

public class Node2 : INode2
{
    public Node2(INode1 node1) { }
}
