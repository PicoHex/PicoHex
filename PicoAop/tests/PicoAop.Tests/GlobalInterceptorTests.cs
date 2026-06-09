namespace PicoAop.Tests;

public class GlobalInterceptorTests : GeneratorTestBase
{
    [Test]
    public async Task GlobalInterceptor_MergesWithPerServiceInt()
    {
        var source = @"
using PicoAop.Abs;

interface ISvc { int Get(); }
class Impl : ISvc { public int Get() => 42; }

class PerSvcInt : InterceptorBase { }
class GlobalInt : InterceptorBase { }

interface IRet
{
    IRet Register<T,TImpl>() where T:class where TImpl:class;
    IRet InterceptBy<T>() where T:class;
    IRet AddInterceptor<T>() where T:class;
}

static class R
{
    static void X(IRet r)
    {
        r.Register<ISvc,Impl>().InterceptBy<PerSvcInt>();
        r.AddInterceptor<GlobalInt>();
    }
}
";
        await RunGenerator(source, async result =>
        {
            var output = GetGeneratedOutput(result);
            await Assert.That(output.Contains("_i0")).IsTrue();
            await Assert.That(output.Contains("_i1")).IsTrue();
            await Assert.That(output.Contains("PerSvcInt")).IsTrue();
            await Assert.That(output.Contains("GlobalInt")).IsTrue();
        });
    }
}
