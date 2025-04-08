// 测试用例类

namespace PicoHex.DI.Sample;

public static class IocTests
{
    // 自举测试
    public static void TestBootstrapping()
    {
        var container = Bootstrap.CreateContainer();

        var provider = container.CreateProvider();
        _ = (ISvcProvider)provider.Resolve(typeof(ISvcProvider));
        Console.WriteLine("Bootstrapping Test Passed");
    }

    // 基础注入测试
    public static void TestBasicInjection()
    {
        var container = Bootstrap.CreateContainer();
        container.RegisterTransient<A>();
        container.RegisterTransient<IB, B>();
        container.RegisterTransient<IC, C>();

        // A的构造函数需要IB参数
        var provider = container.CreateProvider();
        _ = (A)provider.Resolve(typeof(A))!;
        Console.WriteLine("Basic Injection Test Passed");
    }

    // 基础注入测试
    public static void TestIEnumerableInjection()
    {
        var container = Bootstrap.CreateContainer();
        container.RegisterTransient<IA, A>();
        container.RegisterTransient<IB, B>();
        container.RegisterTransient<IC, C>();
        container.RegisterTransient<IService, A>();
        container.RegisterTransient<IService, B>();
        container.RegisterTransient<IService, C>();
        container.RegisterTransient<D>();

        // A的构造函数需要IB参数
        var provider = container.CreateProvider();
        _ = (D)provider.Resolve(typeof(D))!;
        Console.WriteLine("IEnumerable Injection Test Passed");
    }

    // 循环依赖检测测试
    public static void TestCircularDependency()
    {
        var container = Bootstrap.CreateContainer();
        container.RegisterTransient<ICircularA, CircularA>();
        container.RegisterTransient<ICircularB, CircularB>();
        container.RegisterTransient<ICircularC, CircularC>();

        try
        {
            var provider = container.CreateProvider();
            provider.Resolve(typeof(ICircularA));
        }
        catch (InvalidOperationException ex)
        {
            try
            {
                var provider = container.CreateProvider();
                provider.Resolve(typeof(ICircularA));
            }
            catch (InvalidOperationException iex)
            {
                Console.WriteLine(
                    ex.Message.Contains("Circular dependency detected")
                        ? "Circular Dependency Detection Test Passed"
                        : "Circular Test Failed: Wrong exception message"
                );
            }

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
        var container = Bootstrap.CreateContainer();
        container.RegisterTransient<A>();
        container.RegisterTransient<IB, B>();
        container.RegisterTransient<IC, C>();

        try
        {
            var provider = container.CreateProvider();
            provider.Resolve(typeof(A));
            Console.WriteLine("AOT Compatibility Test Passed");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"AOT Test Failed: {ex.Message}");
        }
    }
}

// 测试依赖类

public interface IService;

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

public class C : IC
{
    public C() { }
}

public class D(IEnumerable<IService> services)
{
    public IEnumerable<IService> Services { get; } = services;
}

public interface ICircularA;

public interface ICircularB;

public interface ICircularC;

public class CircularA : ICircularA
{
    public CircularA(ICircularB b) { }
}

public class CircularB : ICircularB
{
    public CircularB(ICircularC c) { }
}

public class CircularC : ICircularC
{
    public CircularC(ICircularA a) { }
}

// 程序入口
public class Program
{
    public static void Main()
    {
        Console.WriteLine($"Running Tests: {DateTime.Now}");

        IocTests.TestBootstrapping();
        IocTests.TestBasicInjection();
        IocTests.TestIEnumerableInjection();
        IocTests.TestCircularDependency();
        IocTests.TestAotCompatibility();

        Console.WriteLine($"Tests finish: {DateTime.Now}");
        Console.ReadLine();
    }
}
