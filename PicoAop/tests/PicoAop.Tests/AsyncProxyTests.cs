namespace PicoAop.Tests;

public class AsyncProxyTests : GeneratorTestBase
{
    [Test]
    public async Task ValueTask_Struct_UsesIInvocationInt()
    {
        var source = @"
using PicoAop.Abs;
using System.Threading.Tasks;

interface IValueSvc
{
    ValueTask<int> GetAsync();
}

class Impl : IValueSvc
{
    public ValueTask<int> GetAsync() => new(42);
}

class MyInterceptor : InterceptorBase { }

interface IDummy
{
    IDummy Register<T, TImpl>() where T : class where TImpl : class;
}

static class Ext
{
    internal static IDummy InterceptBy<T>(this IDummy c) where T : class => c;
}

static class Reg
{
    static void Do(IDummy c)
    {
        c.Register<IValueSvc, Impl>().InterceptBy<MyInterceptor>();
    }
}
";
        await RunGenerator(source, async result =>
        {
            var output = GetGeneratedOutput(result);
            await Assert.That(output.Contains("IInvocation<int>")).IsTrue();
            await Assert.That(output.Contains("InvokeTargetAsync() => _target.GetAsync()")).IsTrue();
        });
    }

    [Test]
    public async Task TaskReturn_Proxy_UsesAsTask()
    {
        var source = @"
using PicoAop.Abs;
using System.Threading.Tasks;

interface ITaskSvc
{
    Task<int> GetAsync();
}

class Impl : ITaskSvc
{
    public Task<int> GetAsync() => Task.FromResult(42);
}

class MyInterceptor : InterceptorBase { }

interface IDummy
{
    IDummy Register<T, TImpl>() where T : class where TImpl : class;
}

static class Ext
{
    internal static IDummy InterceptBy<T>(this IDummy c) where T : class => c;
}

static class Reg
{
    static void Do(IDummy c)
    {
        c.Register<ITaskSvc, Impl>().InterceptBy<MyInterceptor>();
    }
}
";
        await RunGenerator(source, async result =>
        {
            var output = GetGeneratedOutput(result);
            await Assert.That(output.Contains(".AsTask()")).IsTrue();
        });
    }
}
