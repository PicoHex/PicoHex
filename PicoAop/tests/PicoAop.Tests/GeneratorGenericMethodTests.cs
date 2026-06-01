namespace PicoAop.Tests;

public class GeneratorGenericMethodTests : GeneratorTestBase
{
    [Test]
    public async Task GenericMethod_GeneratesGenericInvocationStruct()
    {
        var source = """
            using PicoAop.DI;
            using PicoAop.Abs;
            using PicoDI;
            using PicoDI.Abs;

            public interface ICalc { T Identity<T>(T value); }
            public sealed class Calc : ICalc { public T Identity<T>(T value) => value; }
            public sealed class LogInterceptor : InterceptorBase { }

            public static class Setup
            {
                public static void Configure(SvcContainer c)
                {
                    c.Register<ICalc, Calc>(SvcLifetime.Scoped).InterceptBy<LogInterceptor>();
                }
            }
            """;

        var (compilation, diags) = RunGenerator(source);

        // No errors
        var errors = diags.Where(d => d.Severity == DiagnosticSeverity.Error).ToArray();
        await Assert.That(errors.Length).IsEqualTo(0);

        // Generated code exists
        var trees = compilation.SyntaxTrees.Skip(1).ToList();
        await Assert.That(trees.Count).IsGreaterThan(0);

        var generated = string.Join("", trees.Select(t => t.ToString()));

        // Must contain a generic Invocation struct with type parameter
        await Assert.That(generated.Contains("<T>")).IsTrue();
        await Assert.That(generated.Contains("_Invocation")).IsTrue();
    }

    [Test]
    public async Task AsyncGenericMethod_GeneratesInvocationWithoutError()
    {
        var source = """
            using PicoAop.DI;
            using PicoAop.Abs;
            using PicoDI;
            using PicoDI.Abs;
            using System.Threading.Tasks;

            public interface IAsyncCalc { Task<T> EchoAsync<T>(T value); }
            public sealed class AsyncCalc : IAsyncCalc
            {
                public Task<T> EchoAsync<T>(T value) => Task.FromResult(value);
            }
            public sealed class LogInterceptor : InterceptorBase { }

            public static class Setup
            {
                public static void Configure(SvcContainer c)
                {
                    c.Register<IAsyncCalc, AsyncCalc>(SvcLifetime.Scoped).InterceptBy<LogInterceptor>();
                }
            }
            """;

        var (compilation, diags) = RunGenerator(source);

        var errors = diags.Where(d => d.Severity == DiagnosticSeverity.Error).ToArray();
        await Assert.That(errors.Length).IsEqualTo(0);

        var generated = string.Join("", compilation.SyntaxTrees.Skip(1).Select(t => t.ToString()));
        await Assert.That(generated.Contains("Decorator")).IsTrue();
    }
}
