namespace PicoAop.Tests;

public class GeneratorAsyncTests : GeneratorTestBase
{
    [Test]
    public async Task TaskOfT_GeneratesInvokeAsync()
    {
        var source = """
            using PicoAop.DI;
            using PicoAop.Abs;
            using PicoDI;
            using PicoDI.Abs;
            using System.Threading.Tasks;

            public interface IAsyncSvc { Task<string> GetAsync(); }
            public sealed class AsyncSvc : IAsyncSvc
            {
                public Task<string> GetAsync() => Task.FromResult("ok");
            }
            public sealed class TraceInterceptor : InterceptorBase { }

            public static class Setup
            {
                public static void Configure(SvcContainer c)
                {
                    c.Register<IAsyncSvc, AsyncSvc>(SvcLifetime.Scoped)
                        .InterceptBy<TraceInterceptor>();
                }
            }
            """;

        var (compilation, diags) = RunGenerator(source);
        await Assert.That(diags.Where(d => d.Severity == DiagnosticSeverity.Error)).IsEmpty();
        var generated = string.Join("", compilation.SyntaxTrees.Skip(1).Select(t => t.ToString()));
        await Assert.That(generated.Contains("InvokeAsync(")).IsTrue();
        await Assert.That(generated.Contains("InvokeTargetAsync()")).IsTrue();
    }

    [Test]
    public async Task ValueTask_GeneratesInvokeAsyncVoid()
    {
        var source = """
            using PicoAop.DI;
            using PicoAop.Abs;
            using PicoDI;
            using PicoDI.Abs;
            using System.Threading.Tasks;

            public interface IWorker { ValueTask WorkAsync(); }
            public sealed class Worker : IWorker
            {
                public ValueTask WorkAsync() => ValueTask.CompletedTask;
            }
            public sealed class MetricInterceptor : InterceptorBase { }

            public static class Setup
            {
                public static void Configure(SvcContainer c)
                {
                    c.Register<IWorker, Worker>(SvcLifetime.Scoped)
                        .InterceptBy<MetricInterceptor>();
                }
            }
            """;

        var (compilation, diags) = RunGenerator(source);
        await Assert.That(diags.Where(d => d.Severity == DiagnosticSeverity.Error)).IsEmpty();
        var generated = string.Join("", compilation.SyntaxTrees.Skip(1).Select(t => t.ToString()));
        await Assert.That(generated.Contains("InvokeAsyncVoid(")).IsTrue();
    }

    [Test]
    public async Task Void_GeneratesInvokeVoid()
    {
        var source = """
            using PicoAop.DI;
            using PicoAop.Abs;
            using PicoDI;
            using PicoDI.Abs;

            public interface IVoidSvc { void Execute(); }
            public sealed class VoidSvc : IVoidSvc { public void Execute() { } }
            public sealed class LogInterceptor : InterceptorBase { }

            public static class Setup
            {
                public static void Configure(SvcContainer c)
                {
                    c.Register<IVoidSvc, VoidSvc>(SvcLifetime.Scoped)
                        .InterceptBy<LogInterceptor>();
                }
            }
            """;

        var (compilation, diags) = RunGenerator(source);
        var generated = string.Join("", compilation.SyntaxTrees.Skip(1).Select(t => t.ToString()));
        await Assert.That(generated.Contains("InvokeVoid(inv, static i =>")).IsTrue();
    }

    [Test]
    public async Task SyncResult_GeneratesInvoke()
    {
        var source = """
            using PicoAop.DI;
            using PicoAop.Abs;
            using PicoDI;
            using PicoDI.Abs;

            public interface ICalc { int Add(int a, int b); }
            public sealed class Calc : ICalc { public int Add(int a, int b) => a + b; }
            public sealed class CacheInterceptor : InterceptorBase { }

            public static class Setup
            {
                public static void Configure(SvcContainer c)
                {
                    c.Register<ICalc, Calc>(SvcLifetime.Scoped)
                        .InterceptBy<CacheInterceptor>();
                }
            }
            """;

        var (compilation, diags) = RunGenerator(source);
        var generated = string.Join("", compilation.SyntaxTrees.Skip(1).Select(t => t.ToString()));
        await Assert.That(generated.Contains("_i0.Invoke(inv, static i =>")).IsTrue();
    }

    [Test]
    public async Task AsyncMethod_StructHasInvokeTargetAsync()
    {
        var source = """
            using PicoAop.DI;
            using PicoAop.Abs;
            using PicoDI;
            using PicoDI.Abs;
            using System.Threading.Tasks;

            public interface IRepo { ValueTask<string> FetchAsync(int id); }
            public sealed class Repo : IRepo
            {
                public ValueTask<string> FetchAsync(int id) => ValueTask.FromResult(id.ToString());
            }
            public sealed class CacheInterceptor : InterceptorBase { }

            public static class Setup
            {
                public static void Configure(SvcContainer c)
                {
                    c.Register<IRepo, Repo>(SvcLifetime.Scoped)
                        .InterceptBy<CacheInterceptor>();
                }
            }
            """;

        var (compilation, diags) = RunGenerator(source);
        var generated = string.Join("", compilation.SyntaxTrees.Skip(1).Select(t => t.ToString()));
        await Assert.That(generated.Contains("InvokeTargetAsync()")).IsTrue();
    }
}
