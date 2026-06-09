namespace PicoAop.Tests;

public class GeneratorBasicTests : GeneratorTestBase
{
    [Test]
    public async Task Generator_ProducesInvocationStruct()
    {
        var source =
            @"
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
";
        await RunGenerator(
            source,
            async result =>
            {
                var output = GetGeneratedOutput(result);
                await Assert
                    .That(output.Contains("Invocation_IMyService_GetValue_MyInterceptor"))
                    .IsTrue();
            }
        );
    }

    [Test]
    public async Task MultiChain_InterceptBy_DoesNotReportPico101()
    {
        var source =
            @"
using PicoAop.Abs;

interface IMyService
{
    int GetValue();
}

class MyService : IMyService
{
    public int GetValue() => 42;
}

class N1 : InterceptorBase { }
class N2 : InterceptorBase { }

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
        c.Register<IMyService, MyService>().InterceptBy<N1>().InterceptBy<N2>();
    }
}
";
        var diags = RunAndGetDiagnostics(source);
        var hasPico101 = diags.Any(d => d.Id == "PICO101");
        await Assert.That(hasPico101).IsFalse();
    }
}
