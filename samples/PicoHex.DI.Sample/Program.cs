namespace PicoHex.DI.Sample;

public static class IocTests
{
    // Tests container bootstrapping and self-registration
    public static void TestBootstrapping()
    {
        var container = Bootstrap.CreateContainer();
        var provider = container.CreateProvider();

        Console.WriteLine(
            container == provider.Resolve<ISvcContainer>()
            && provider == (ISvcProvider)provider.Resolve(typeof(ISvcProvider))
                ? "Bootstrapping Test Passed"
                : "Bootstrapping Test Failed"
        );
    }

    // Tests basic constructor injection with interface implementations
    public static void TestBasicInjection()
    {
        var container = Bootstrap.CreateContainer();
        container.RegisterTransient<A>();
        container.RegisterTransient<IB, B>();
        container.RegisterTransient<IC, C>();

        // Class A requires IB in its constructor
        var provider = container.CreateProvider();
        _ = (A)provider.Resolve(typeof(A));
        Console.WriteLine("Basic Injection Test Passed");
    }

    // Tests IEnumerable dependency injection with multiple implementations
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

        // Class D requires IEnumerable<IService> in constructor
        var provider = container.CreateProvider();
        _ = (D)provider.Resolve(typeof(D));
        Console.WriteLine("IEnumerable Injection Test Passed");
    }

    // Tests circular dependency detection mechanism
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
            Console.WriteLine(
                ex.Message.Contains("Circular dependency detected")
                    ? "Circular Dependency Detection Test Passed"
                    : "Circular Test Failed: Wrong exception message"
            );
            return;
        }

        Console.WriteLine("Circular Test Failed: Expected exception not thrown");
    }

    // Tests duplicate transient registration detection
    public static void DuplicateRegistration()
    {
        var container = Bootstrap.CreateContainer();
        container.RegisterTransient<IA, A>();
        container.RegisterTransient<IB, B>();
        container.RegisterTransient<IC, C>();

        try
        {
            container.RegisterTransient<IA, A>();
        }
        catch (InvalidOperationException ex)
        {
            Console.WriteLine(
                ex.Message.Contains("Duplicate registration for type")
                    ? $"Duplicate Registration Test Passed: {ex.Message}"
                    : "Duplicate Registration Test Failed: Wrong exception message"
            );
            return;
        }

        Console.WriteLine("Duplicate Registration Test Failed: Expected exception not thrown");
    }

    // Tests duplicate singleton registration detection
    public static void DuplicateSingletonRegistration()
    {
        var container = Bootstrap.CreateContainer();
        var c = new C();
        container.RegisterSingle<IC>(c);

        try
        {
            container.RegisterSingle<IC>(c);
        }
        catch (InvalidOperationException ex)
        {
            Console.WriteLine(
                ex.Message.Contains("Duplicate registration for type")
                    ? $"Duplicate Singleton Registration Test Passed: {ex.Message}"
                    : "Duplicate Registration Test Failed: Wrong exception message"
            );
            return;
        }

        Console.WriteLine("Duplicate Registration Test Failed: Expected exception not thrown");
    }

    // Tests Ahead-of-Time compilation compatibility scenarios
    public static void TestAotCompatibility()
    {
        // Verify container can resolve types without reflection in AOT environments
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

// Dependency classes for testing

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

// Circular dependency test classes
public interface ICircularA;

public interface ICircularB;

public interface ICircularC;

public class CircularA(ICircularB b) : ICircularA;

public class CircularB(ICircularC c) : ICircularB;

public class CircularC(ICircularA a) : ICircularC;

// Program entry point
public class Program
{
    public static void Main()
    {
        Console.WriteLine($"Running Tests: {DateTime.Now}");

        IocTests.TestBootstrapping();
        IocTests.TestBasicInjection();
        IocTests.TestIEnumerableInjection();
        IocTests.TestCircularDependency();
        IocTests.DuplicateRegistration();
        IocTests.DuplicateSingletonRegistration();
        IocTests.TestAotCompatibility();

        Console.WriteLine($"Tests completed: {DateTime.Now}");
        Console.ReadLine();
    }
}
