namespace PicoAot.Tests;

public class GeneratorBasicTests : GeneratorTestBase
{
    [Test]
    public async Task Generator_ProducesInvocationStruct()
    {
        var source = @"
using PicoAot.Abs;

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
        await RunGenerator(source, async result =>
        {
            var output = GetGeneratedOutput(result);
            Console.WriteLine("=== SG OUTPUT ===");
            Console.WriteLine(output);
            await Assert.That(output.Contains("Invocation_IMyService_GetValue")).IsTrue();
        });
    }
}
