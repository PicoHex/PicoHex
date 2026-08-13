namespace PicoAop.Tests;

public class WithoutInterceptorTests : GeneratorTestBase
{
    [Test]
    public async Task WithoutInterceptor_RemovesInterceptorFromProxy()
    {
        var source = """
            using PicoAop.Abs;
            interface IMySvc { int Get(); }
            class MySvc : IMySvc { public int Get() => 1; }
            class IntA : InterceptorBase { }
            class IntB : InterceptorBase { }

            interface IDummy
            {
                IDummy Register<T, TImpl>() where T : class where TImpl : class;
            }
            static class Ext
            {
                internal static IDummy InterceptBy<T>(this IDummy c) where T : class => c;
                internal static IDummy WithoutInterceptor<T>(this IDummy c) where T : class => c;
            }
            static class Reg
            {
                static void Do(IDummy c)
                {
                    c.Register<IMySvc, MySvc>()
                        .InterceptBy<IntA>()
                        .InterceptBy<IntB>()
                        .WithoutInterceptor<IntA>();
                }
            }
            """;

        await RunGenerator(
            source,
            async result =>
            {
                var output = GetGeneratedOutput(result);
                // Only IntB survives — the proxy must hold exactly one
                // interceptor field and the wrapper must take exactly one
                // interceptor parameter.
                await Assert.That(output.Contains("_i0;")).IsTrue();
                await Assert.That(output.Contains("_i1;")).IsFalse();
                await Assert.That(output.Contains("IntA")).IsFalse();
                await Assert.That(output.Contains("IntB")).IsTrue();
            }
        );
    }

    [Test]
    public async Task WithoutInterceptors_SuppressesProxyGeneration()
    {
        var source = """
            using PicoAop.Abs;
            interface IMySvc { int Get(); }
            class MySvc : IMySvc { public int Get() => 1; }
            class IntA : InterceptorBase { }

            interface IDummy
            {
                IDummy Register<T, TImpl>() where T : class where TImpl : class;
            }
            static class Ext
            {
                internal static IDummy InterceptBy<T>(this IDummy c) where T : class => c;
                internal static IDummy WithoutInterceptors(this IDummy c) => c;
            }
            static class Reg
            {
                static void Do(IDummy c)
                {
                    c.Register<IMySvc, MySvc>().InterceptBy<IntA>().WithoutInterceptors();
                }
            }
            """;

        await RunGenerator(
            source,
            async result =>
            {
                var output = GetGeneratedOutput(result);
                await Assert.That(output.Contains("Intercepted_")).IsFalse();
                await Assert.That(output.Contains("Wrap_")).IsFalse();
            }
        );
    }

    [Test]
    public async Task AllInterceptorsRemoved_SuppressesProxyGeneration()
    {
        var source = """
            using PicoAop.Abs;
            interface IMySvc { int Get(); }
            class MySvc : IMySvc { public int Get() => 1; }
            class IntA : InterceptorBase { }

            interface IDummy
            {
                IDummy Register<T, TImpl>() where T : class where TImpl : class;
            }
            static class Ext
            {
                internal static IDummy InterceptBy<T>(this IDummy c) where T : class => c;
                internal static IDummy WithoutInterceptor<T>(this IDummy c) where T : class => c;
            }
            static class Reg
            {
                static void Do(IDummy c)
                {
                    c.Register<IMySvc, MySvc>().InterceptBy<IntA>().WithoutInterceptor<IntA>();
                }
            }
            """;

        await RunGenerator(
            source,
            async result =>
            {
                var output = GetGeneratedOutput(result);
                await Assert.That(output.Contains("Intercepted_")).IsFalse();
                await Assert.That(output.Contains("Wrap_")).IsFalse();
            }
        );
    }
}
