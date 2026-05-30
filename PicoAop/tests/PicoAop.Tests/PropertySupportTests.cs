namespace PicoAop.Tests;

public sealed class PropertySupportTests : GeneratorTestBase
{
    [Test]
    public async Task InterfaceWithProperty_GeneratesDelegatingDecorator()
    {
        var source = """
            using PicoAop.DI;
            using PicoAop.Abs;
            using PicoDI;
            using PicoDI.Abs;

            public interface IFoo
            {
                string Name { get; set; }
                int Value { get; }
                void Do();
            }
            public sealed class Foo : IFoo
            {
                public string Name { get; set; } = "";
                public int Value => 42;
                public void Do() { }
            }
            public sealed class LogInterceptor : InterceptorBase { }

            public static class Setup
            {
                public static void Configure(SvcContainer c)
                {
                    c.Register<IFoo, Foo>(SvcLifetime.Scoped).InterceptBy<LogInterceptor>();
                }
            }
            """;

        var (compilation, diags) = RunGenerator(source);

        var errors = compilation
            .GetDiagnostics()
            .Where(d => d.Severity == DiagnosticSeverity.Error)
            .ToList();

        foreach (var e in errors)
            Console.WriteLine($"  error: {e.GetMessage()}");

        await Assert.That(errors).IsEmpty();
    }

    [Test]
    public async Task InterfaceWithInitProperty_GeneratesDelegatingDecorator()
    {
        var source = """
            using PicoAop.DI;
            using PicoAop.Abs;
            using PicoDI;
            using PicoDI.Abs;

            public interface IFoo
            {
                string Tag { get; init; }
                void Do();
            }
            public sealed class Foo : IFoo
            {
                public string Tag { get; init; } = "x";
                public void Do() { }
            }
            public sealed class LogInterceptor : InterceptorBase { }

            public static class Setup
            {
                public static void Configure(SvcContainer c)
                {
                    c.Register<IFoo, Foo>(SvcLifetime.Scoped).InterceptBy<LogInterceptor>();
                }
            }
            """;

        var (compilation, diags) = RunGenerator(source);

        var errors = compilation
            .GetDiagnostics()
            .Where(d => d.Severity == DiagnosticSeverity.Error)
            .ToList();

        foreach (var e in errors)
            Console.WriteLine($"  error: {e.GetMessage()}");

        await Assert.That(errors).IsEmpty();
    }

    [Test]
    public async Task InterfaceWithOnlyProperties_CompilesWithoutError()
    {
        var source = """
            using PicoAop.DI;
            using PicoAop.Abs;
            using PicoDI;
            using PicoDI.Abs;

            public interface IFoo
            {
                string Name { get; }
                int Count { get; set; }
            }
            public sealed class Foo : IFoo
            {
                public string Name => "x";
                public int Count { get; set; }
            }
            public sealed class LogInterceptor : InterceptorBase { }

            public static class Setup
            {
                public static void Configure(SvcContainer c)
                {
                    c.Register<IFoo, Foo>(SvcLifetime.Scoped).InterceptBy<LogInterceptor>();
                }
            }
            """;

        var (compilation, diags) = RunGenerator(source);

        var errors = compilation
            .GetDiagnostics()
            .Where(d => d.Severity == DiagnosticSeverity.Error)
            .ToList();

        foreach (var e in errors)
            Console.WriteLine($"  error: {e.GetMessage()}");

        await Assert.That(errors).IsEmpty();
    }
}
