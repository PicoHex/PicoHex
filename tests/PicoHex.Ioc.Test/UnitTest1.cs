using PicoHex.IoC;
using PicoHex.IoC.Abstractions;
using Xunit.Abstractions;

namespace PicoHex.Ioc.Test;

public class DependencyInjectionTests(ITestOutputHelper testOutputHelper)
{
    [Fact]
    public void Register_And_Resolve_PerThread()
    {
        try
        {
            var svcRegistry = Bootstrap.CreateContainer();
            var svcProvider = svcRegistry.CreateProvider();

            svcRegistry.Register<IService, ServiceImpl>(SvcLifetime.PerThread);

            IService? s1 = null;
            IService? s2 = null;
            IService? s3 = null;

            var t1 = new Thread(() =>
            {
                s1 = svcProvider.Resolve<IService>();
                s2 = svcProvider.Resolve<IService>();
            });
            var t2 = new Thread(() =>
            {
                s3 = svcProvider.Resolve<IService>();
            });

            t1.Start();
            t2.Start();
            t1.Join();
            t2.Join();

            Assert.NotNull(s1);
            Assert.NotNull(s2);
            Assert.NotNull(s3);
            Assert.Same(s1, s2);
            Assert.NotSame(s1, s3);
        }
        catch (Exception e)
        {
            testOutputHelper.WriteLine(e.ToString());
            throw;
        }
    }

    [Fact]
    public void Register_And_Resolve_Transient()
    {
        var svcRegistry = Bootstrap.CreateContainer();
        var svcProvider = svcRegistry.CreateProvider();

        svcRegistry.Register<IService, ServiceImpl>(SvcLifetime.Transient);
        var s1 = svcProvider.Resolve<IService>();
        var s2 = svcProvider.Resolve<IService>();

        Assert.NotNull(s1);
        Assert.NotNull(s2);
        Assert.NotSame(s1, s2);
    }

    [Fact]
    public void Register_And_Resolve_TransientA()
    {
        var svcRegistry = Bootstrap.CreateContainer();
        var svcProvider = svcRegistry.CreateProvider();

        svcRegistry.Register<IService, ServiceImpl>(SvcLifetime.Transient);
        svcRegistry.Register<IServiceA, ServiceAImpl>(SvcLifetime.Transient);
        svcRegistry.Register<IServiceB, ServiceBImpl>(SvcLifetime.Transient);
        var s1 = svcProvider.Resolve<IServiceA>();
        var s2 = svcProvider.Resolve<IServiceB>();

        Assert.NotNull(s1);
        Assert.NotNull(s2);
        Assert.NotSame(s1, s2);
    }

    [Fact]
    public void Register_And_Resolve_Singleton()
    {
        var svcRegistry = Bootstrap.CreateContainer();
        var svcProvider = svcRegistry.CreateProvider();

        svcRegistry.Register<IService, ServiceImpl>(SvcLifetime.Singleton);
        var s1 = svcProvider.Resolve<IService>();
        var s2 = svcProvider.Resolve<IService>();

        Assert.Same(s1, s2);
    }

    [Fact]
    public void Register_And_Resolve_Scoped()
    {
        var svcRegistry = Bootstrap.CreateContainer();
        var svcProvider = svcRegistry.CreateProvider();

        svcRegistry.Register<IService, ServiceImpl>(SvcLifetime.Scoped);
        IService? s1;
        using (var scope1 = svcProvider.CreateScope())
        {
            s1 = scope1.Resolve<IService>();
            var s2 = scope1.Resolve<IService>();

            Assert.Same(s1, s2);
        }

        using (var scope2 = svcProvider.CreateScope())
        {
            var s3 = scope2.Resolve<IService>();
            var s4 = scope2.Resolve<IService>();

            Assert.Same(s3, s4);
            Assert.NotSame(s3, s1);
        }
    }

    [Fact]
    public void Throws_When_Not_Registered()
    {
        var svcRegistry = Bootstrap.CreateContainer();
        var svcProvider = svcRegistry.CreateProvider();

        Assert.Throws<InvalidOperationException>(() => svcProvider.Resolve<IService>());
    }

    [Fact]
    public void Throws_When_Circular_Dependency()
    {
        var svcRegistry = Bootstrap.CreateContainer();
        var svcProvider = svcRegistry.CreateProvider();

        svcRegistry.Register<INode1, Node1>(SvcLifetime.Transient);
        svcRegistry.Register<INode2, Node2>(SvcLifetime.Transient);
        svcRegistry.Register<INode3, Node3>(SvcLifetime.Transient);

        Assert.Throws<InvalidOperationException>(() => svcProvider.Resolve<INode1>());
    }
}

public interface IService { }

public class ServiceImpl : IService { }

public interface IServiceA { }

public class ServiceAImpl(IService service) : IServiceA { }

public interface IServiceB { }

public class ServiceBImpl : IServiceB
{
    private readonly IServiceA? _serviceA;
    private readonly IServiceC? _serviceC;

    public ServiceBImpl(ISvcContainer container, ISvcProvider provider)
    {
        _serviceA = provider.Resolve<IServiceA>();
        container.Register<IServiceC, ServiceCImpl>(SvcLifetime.Transient);
        _serviceC = provider.Resolve<IServiceC>();
    }
}

public interface IServiceC { }

public class ServiceCImpl() : IServiceC { }

public interface INode1 { }

public interface INode2 { }

public interface INode3 { }

public class Node1 : INode1
{
    public Node1(INode3 node3) { }
}

public class Node2 : INode2
{
    public Node2(INode1 node1) { }
}

public class Node3 : INode3
{
    public Node3(INode2 node2) { }
}
