namespace PicoAop.Tests;

public class MultiChainTests : GeneratorTestBase
{
    [Test]
    public async Task MultiChain_GeneratesOneProxy()
    {
        var source = @"
using PicoAop.Abs;
interface IMySvc { int GetValue(); }
class MySvc : IMySvc { public int GetValue() => 42; }
class N1 : InterceptorBase { }
class N2 : InterceptorBase { }
interface IRet { IRet Register<T,TImpl>() where T:class where TImpl:class; IRet InterceptBy<T>() where T:class; }
static class R { static void X(IRet r) { r.Register<IMySvc,MySvc>().InterceptBy<N1>().InterceptBy<N2>(); } }
";
        await RunGenerator(source, async result =>
        {
            var output = GetGeneratedOutput(result);
            // All interceptors share one struct
            await Assert.That(output.Contains("_i0")).IsTrue();
            await Assert.That(output.Contains("_i1")).IsTrue();
            // Proxy has both interceptors in constructor
            await Assert.That(output.Contains("_i0 = i0")).IsTrue();
            await Assert.That(output.Contains("_i1 = i1")).IsTrue();
        });
    }

    [Test]
    public async Task SingleInterceptor_GeneratesI0()
    {
        var source = @"
using PicoAop.Abs;
interface IMySvc { int GetValue(); }
class MySvc : IMySvc { public int GetValue() => 42; }
class N1 : InterceptorBase { }
interface IRet { IRet Register<T,TImpl>() where T:class where TImpl:class; IRet InterceptBy<T>() where T:class; }
static class R { static void X(IRet r) { r.Register<IMySvc,MySvc>().InterceptBy<N1>(); } }
";
        await RunGenerator(source, async result =>
        {
            var output = GetGeneratedOutput(result);
            await Assert.That(output.Contains("_i0")).IsTrue();
            await Assert.That(output.Contains("struct Invocation_IMySvc_GetValue_N1")).IsTrue();
        });
    }
}
