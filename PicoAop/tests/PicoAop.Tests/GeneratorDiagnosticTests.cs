namespace PicoAop.Tests;

public class GeneratorDiagnosticTests : GeneratorTestBase
{
    [Test]
    public async Task InterceptByNonInterceptor_EmitsPICO010()
    {
        var source = """
            using PicoAop.DI;
            using PicoAop.Abs;
            using PicoDI;
            using PicoDI.Abs;

            public interface IFoo { void Do(); }
            public sealed class Foo : IFoo { public void Do() { } }

            public static class Setup
            {
                public static void Configure(SvcContainer c)
                {
                    c.Register<IFoo, Foo>(SvcLifetime.Scoped).InterceptBy<string>();
                }
            }
            """;

        var (compilation, diags) = RunGenerator(source);
        var pico010 = diags.FirstOrDefault(
            d => d.Id == "PICO010" && d.Severity == DiagnosticSeverity.Error
        );
        await Assert.That(pico010).IsNotNull();
    }

    [Test]
    public async Task ZeroInterceptorsMatched_EmitsPICO012()
    {
        var source = """
            using PicoAop.DI;
            using PicoAop.Abs;
            using PicoDI;
            using PicoDI.Abs;

            public interface IBar { int Get(); }
            public sealed class Bar : IBar { public int Get() => 1; }
            public sealed class Interceptor1 : InterceptorBase { }

            public static class Setup
            {
                public static void Configure(SvcContainer c)
                {
                    c.Register<IBar, Bar>(SvcLifetime.Scoped)
                        .InterceptBy<Interceptor1>()
                        .WithoutInterceptor<Interceptor1>();
                }
            }
            """;

        var (compilation, diags) = RunGenerator(source);
        var pico012 = diags.FirstOrDefault(
            d => d.Id == "PICO012" && d.Severity == DiagnosticSeverity.Warning
        );
        await Assert.That(pico012).IsNotNull();
    }

    [Test]
    public async Task AllDiagnosticDescriptors_HaveCorrectIds()
    {
        await Assert.That(InterceptorDiagParams.InterceptorTypeMismatch.Id).IsEqualTo("PICO010");
        await Assert.That(InterceptorDiagParams.FilterRequiresInterface.Id).IsEqualTo("PICO011");
        await Assert.That(InterceptorDiagParams.ZeroInterceptorsMatched.Id).IsEqualTo("PICO012");
        await Assert
            .That(InterceptorDiagParams.ConflictingInterceptorDeclaration.Id)
            .IsEqualTo("PICO013");
        await Assert.That(InterceptorDiagParams.AmbiguousInterceptBy.Id).IsEqualTo("PICO014");
    }
}
