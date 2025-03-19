// 测试用例类
public static class IocTests
{
    // 自举测试
    public static void TestBootstrapping()
    {
        var container = new SvcContainer();
        container.Register<ISvcProvider, SvcContainer>();

        var subContainer = (ISvcProvider)container.GetService(typeof(ISvcProvider));
        Console.WriteLine("Bootstrapping Test Passed");
    }

    // 基础注入测试
    public static void TestBasicInjection()
    {
        var container = new SvcContainer();
        container.Register<A>();
        container.Register<IB, B>();
        container.Register<IC, C>();

        // A的构造函数需要IB参数
        var a = (A)container.GetService(typeof(A));
        Console.WriteLine("Basic Injection Test Passed");
    }

    // 循环依赖检测测试
    public static void TestCircularDependency()
    {
        var container = new SvcContainer();
        container.Register<ICircularA, CircularA>();
        container.Register<ICircularB, CircularB>();

        try
        {
            container.GetService(typeof(ICircularA));
        }
        catch (InvalidOperationException ex)
        {
            Console.WriteLine(
                ex.Message.Contains("Circular dependency detected")
                    ? "Circular Dependency Detection Test Passed"
                    : "Circular Test Failed: Wrong exception message"
            );
            return;
        }

        Console.WriteLine("Circular Test Failed: Expected exception not thrown");
    }

    // AOT兼容性测试
    public static void TestAotCompatibility()
    {
        // AOT环境下需要确保容器能正确执行
        var container = new SvcContainer();
        container.Register<A>();
        container.Register<IB, B>();
        container.Register<IC, C>();

        try
        {
            var service = container.GetService(typeof(A));
            Console.WriteLine("AOT Compatibility Test Passed");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"AOT Test Failed: {ex.Message}");
        }
    }
}

// 测试依赖类

public interface IB;

public interface IC;

public interface ICircularA;

public interface ICircularB;

public class A(IB b)
{
    public IB B { get; } = b;
}

public class B(IC c) : IB
{
    public IC C { get; } = c;
}

public class C : IC
{
    public C() { }
}

public class CircularA : ICircularA
{
    public CircularA(ICircularB b) { }
}

public class CircularB : ICircularB
{
    public CircularB(ICircularA a) { }
}

// 程序入口
public class Program
{
    public static void Main()
    {
        Console.WriteLine("Running Tests:");

        IocTests.TestBootstrapping();
        IocTests.TestBasicInjection();
        IocTests.TestCircularDependency();
        IocTests.TestAotCompatibility();

        Console.ReadLine();
    }
}
