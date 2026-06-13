namespace PicoAop.Tests;

public class ClassInterceptionTests : GeneratorTestBase
{
    [Test]
    public async Task ClassWithVirtualMethod_ProxyUsesOverride()
    {
        // 🔴 RED: Currently the proxy emits "public T Method()" without "override".
        // For class types with virtual methods, this should be "public override T Method()"
        // so that virtual dispatch correctly routes calls through the proxy.
        var source = """
            using PicoAop.Abs;
            class MyBaseClass
            {
                public virtual int GetValue() => 42;
            }
            class MyInterceptor : InterceptorBase { }
            interface IDummyContainer
            {
                IDummyContainer Register<T, TImpl>() where T : class where TImpl : class;
            }
            static class Ext
            {
                internal static IDummyContainer InterceptBy<T>(this IDummyContainer c) where T : class => c;
            }
            static class Registration
            {
                static void Do(IDummyContainer c)
                {
                    c.Register<MyBaseClass, MyBaseClass>().InterceptBy<MyInterceptor>();
                }
            }
            """;
        await RunGenerator(
            source,
            async result =>
            {
                var output = GetGeneratedOutput(result);
                Console.WriteLine("=== GENERATED OUTPUT ===");
                Console.WriteLine(output);
                // The generated proxy should have "public override" for the virtual method
                await Assert.That(output.Contains("public override int GetValue(")).IsTrue();
            }
        );
    }

    [Test]
    public async Task InterfaceMethod_StillUsesPublic()
    {
        // Interface interception should still use "public" (not "public override")
        var source = """
            using PicoAop.Abs;
            interface IMyService
            {
                int GetValue();
            }
            class MyService : IMyService
            {
                public int GetValue() => 42;
            }
            class MyInterceptor : InterceptorBase { }
            interface IDummyContainer
            {
                IDummyContainer Register<T, TImpl>() where T : class where TImpl : class;
            }
            static class Ext
            {
                internal static IDummyContainer InterceptBy<T>(this IDummyContainer c) where T : class => c;
            }
            static class Registration
            {
                static void Do(IDummyContainer c)
                {
                    c.Register<IMyService, MyService>().InterceptBy<MyInterceptor>();
                }
            }
            """;
        await RunGenerator(
            source,
            async result =>
            {
                var output = GetGeneratedOutput(result);
                Console.WriteLine("=== GENERATED OUTPUT ===");
                Console.WriteLine(output);
                // Interface should keep "public" (no "override")
                await Assert.That(output.Contains("public override")).IsFalse();
                await Assert.That(output.Contains("public int GetValue(")).IsTrue();
            }
        );
    }
}
