using PicoHex.DependencyInjection;

namespace TestProject1;

public class DiContainerTests
{
    private DiContainer _container;

    public DiContainerTests()
    {
        _container = new DiContainer();
    }

    [Fact]
    public void Register_And_Resolve_Transient()
    {
        _container.Register<IService, ServiceImpl>(Lifetime.Transient);
        var s1 = _container.Resolve<IService>();
        var s2 = _container.Resolve<IService>();

        Assert.NotNull((object?)s1);
        Assert.NotNull((object?)s2);
        Assert.NotSame(s1, s2);
    }

    [Fact]
    public void Register_And_Resolve_Singleton()
    {
        _container.Register<IService, ServiceImpl>(Lifetime.Singleton);
        var s1 = _container.Resolve<IService>();
        var s2 = _container.Resolve<IService>();

        Assert.Same(s1, s2);
    }

    [Fact]
    public void Register_And_Resolve_Scoped()
    {
        _container.Register<IService, ServiceImpl>(Lifetime.Scoped);
        IService s1,
            s2,
            s3,
            s4;
        using (var scope1 = _container.CreateScope())
        {
            s1 = _container.Resolve<IService>();
            s2 = _container.Resolve<IService>();

            Assert.Same(s1, s2);
        }

        using (var scope2 = _container.CreateScope())
        {
            s3 = _container.Resolve<IService>();
            s4 = _container.Resolve<IService>();

            Assert.Same(s3, s4);
            Assert.NotSame(s3, s1);
        }
    }

    [Fact]
    public void Register_And_Resolve_PerThread()
    {
        _container.Register<IService, ServiceImpl>(Lifetime.PerThread);

        IService s1 = null;
        IService s2 = null;

        var t1 = new Thread(() =>
        {
            s1 = _container.Resolve<IService>();
        });
        var t2 = new Thread(() =>
        {
            s2 = _container.Resolve<IService>();
        });

        t1.Start();
        t2.Start();
        t1.Join();
        t2.Join();

        Assert.NotNull((object?)s1);
        Assert.NotNull((object?)s2);
        Assert.NotSame(s1, s2);
    }

    [Fact]
    public void Throws_When_Not_Registered()
    {
        Assert.Throws<System.InvalidOperationException>(() => _container.Resolve<IService>());
    }

    [Fact]
    public void Cyclic_Dependency_Detected()
    {
        _container.Register<INode1, Node1>();
        _container.Register<INode2, Node2>();

        Assert.Throws<System.InvalidOperationException>(() => _container.Resolve<INode1>());
    }
}

// filepath: /path/to/TestTypes.cs
public interface IService { }

public class ServiceImpl : IService { }

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
