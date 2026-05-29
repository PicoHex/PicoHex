namespace PicoAop.Tests;

public class GeneratorGlobalTests : GeneratorTestBase
{
    [Test]
    public async Task AddInterceptor_CombinedWithPerService_GeneratesBoth()
    {
        var source = """
            using PicoAop.DI;
            using PicoAop.Abs;
            using PicoDI;
            using PicoDI.Abs;

            public interface IFoo { void Do(); }
            public sealed class Foo : IFoo { public void Do() { } }
            public sealed class GlobalInterceptor : InterceptorBase { }
            public sealed class LocalInterceptor : InterceptorBase { }

            public static class Setup
            {
                public static void Configure(SvcContainer c)
                {
                    c.AddInterceptor<GlobalInterceptor>();
                    c.Register<IFoo, Foo>(SvcLifetime.Scoped)
                        .InterceptBy<LocalInterceptor>();
                }
            }
            """;

        var (compilation, diags) = RunGenerator(source);
        var generated = string.Join("", compilation.SyntaxTrees.Skip(1).Select(t => t.ToString()));
        await Assert.That(generated.Contains("LocalInterceptorDecorator")).IsTrue();
    }
}
