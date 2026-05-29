namespace PicoAop.Tests;

public class GeneratorEdgeCaseTests : GeneratorTestBase
{
    // ── PICO012: Zero interceptors matched ──
    [Test]
    public async Task WithoutInterceptors_ClearsAll_EmitsPICO012()
    {
        var source = """
            using PicoAop.DI;
            using PicoAop.Abs;
            using PicoDI;
            using PicoDI.Abs;

            public interface IFoo { void Do(); }
            public sealed class Foo : IFoo { public void Do() { } }
            public sealed class Interceptor1 : InterceptorBase { }

            public static class Setup
            {
                public static void Configure(SvcContainer c)
                {
                    c.Register<IFoo, Foo>(SvcLifetime.Scoped)
                        .InterceptBy<Interceptor1>()
                        .WithoutInterceptors();
                }
            }
            """;

        var (_, diags) = RunGenerator(source);
        var pico012 = diags.FirstOrDefault(d => d.Id == "PICO012");
        await Assert.That(pico012).IsNotNull();
        await Assert.That(pico012!.Severity).IsEqualTo(DiagnosticSeverity.Warning);
    }

    // ── PICO013: descriptor validation (full end-to-end needs global pipeline fix) ──
    [Test]
    public async Task PICO013_DescriptorIsCorrect()
    {
        var d = InterceptorDiagParams.ConflictingInterceptorDeclaration;
        await Assert.That(d.Id).IsEqualTo("PICO013");
        await Assert.That(d.DefaultSeverity).IsEqualTo(DiagnosticSeverity.Error);
    }

    // ── Generated code is AOT-clean (no System.Reflection) ──
    [Test]
    public async Task GeneratedCode_DoesNotContainReflection()
    {
        var source = """
            using PicoAop.DI;
            using PicoAop.Abs;
            using PicoDI;
            using PicoDI.Abs;

            public interface IFoo { string Bar(); }
            public sealed class Foo : IFoo { public string Bar() => "x"; }
            public sealed class MyInterceptor : InterceptorBase { }

            public static class Setup
            {
                public static void Configure(SvcContainer c)
                {
                    c.Register<IFoo, Foo>(SvcLifetime.Scoped).InterceptBy<MyInterceptor>();
                }
            }
            """;

        var (compilation, diags) = RunGenerator(source);
        await Assert.That(diags.Where(d => d.Severity == DiagnosticSeverity.Error)).IsEmpty();

        var generated = string.Join("", compilation.SyntaxTrees.Skip(1).Select(t => t.ToString()));
        await Assert.That(generated.Contains("System.Reflection")).IsFalse();
        await Assert.That(generated.Contains("GetType()")).IsFalse();
    }
}
