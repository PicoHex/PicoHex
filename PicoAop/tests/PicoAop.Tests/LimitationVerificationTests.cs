namespace PicoAop.Tests;

public sealed class LimitationVerificationTests : GeneratorTestBase
{
    // ── #1: Property support (NOW FIXED) ──

    [Test]
    public async Task Property_Interface_NowCompiles()
    {
        var source = """
            using PicoAop.DI;
            using PicoAop.Abs;
            using PicoDI;
            using PicoDI.Abs;

            public interface IFoo { string Name { get; } void Do(); }
            public sealed class Foo : IFoo
            {
                public string Name => "x";
                public void Do() { }
            }
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

        var errors = compilation
            .GetDiagnostics()
            .Where(d => d.Severity == DiagnosticSeverity.Error)
            .ToList();

        await Assert.That(errors).IsEmpty();
    }

    // ── #2: Method overload collision ──

    [Test]
    public async Task MethodOverload_CausesDuplicateTypeName()
    {
        var source = """
            using PicoAop.DI;
            using PicoAop.Abs;
            using PicoDI;
            using PicoDI.Abs;

            public interface IFoo
            {
                void Do();
                void Do(int x);
            }
            public sealed class Foo : IFoo
            {
                public void Do() { }
                public void Do(int x) { }
            }
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

        var compilationErrors = compilation
            .GetDiagnostics()
            .Where(d => d.Severity == DiagnosticSeverity.Error)
            .ToList();

        foreach (var e in compilationErrors)
            Console.WriteLine($"  error: {e.GetMessage()}");

        await Assert
            .That(
                compilationErrors.Any(
                    e =>
                        e.GetMessage().Contains("already contains a definition")
                        || e.GetMessage().Contains("duplicate")
                )
            )
            .IsTrue();
    }

    // ── #10: init-only property (NOW COMPILES but setter is no-op) ──

    [Test]
    public async Task InitOnlyProperty_NowCompiles()
    {
        var source = """
            using PicoAop.DI;
            using PicoAop.Abs;
            using PicoDI;
            using PicoDI.Abs;

            public interface IFoo { string Tag { get; init; } void Do(); }
            public sealed class Foo : IFoo
            {
                public string Tag { get; init; } = "x";
                public void Do() { }
            }
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

        var errors = compilation
            .GetDiagnostics()
            .Where(d => d.Severity == DiagnosticSeverity.Error)
            .ToList();

        await Assert.That(errors).IsEmpty();
    }

    // ── #3: ref parameter ──

    [Test]
    public async Task RefParameter_CausesCompilationError()
    {
        var source = """
            using PicoAop.DI;
            using PicoAop.Abs;
            using PicoDI;
            using PicoDI.Abs;

            public interface IFoo { void Parse(ref int x); }
            public sealed class Foo : IFoo
            {
                public void Parse(ref int x) { x = 42; }
            }
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

        var compilationErrors = compilation
            .GetDiagnostics()
            .Where(d => d.Severity == DiagnosticSeverity.Error)
            .ToList();

        foreach (var e in compilationErrors)
            Console.WriteLine($"  error: {e.GetMessage()}");

        await Assert.That(compilationErrors.Count).IsGreaterThan(0);
    }

    // ── #4: Generic method ──

    [Test]
    public async Task GenericMethod_CausesCompilationError()
    {
        var source = """
            using PicoAop.DI;
            using PicoAop.Abs;
            using PicoDI;
            using PicoDI.Abs;

            public interface IFoo { T Get<T>(string key); }
            public sealed class Foo : IFoo
            {
                public T Get<T>(string key) => default!;
            }
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

        var compilationErrors = compilation
            .GetDiagnostics()
            .Where(d => d.Severity == DiagnosticSeverity.Error)
            .ToList();

        foreach (var e in compilationErrors)
            Console.WriteLine($"  error: {e.GetMessage()}");

        await Assert.That(compilationErrors.Count).IsGreaterThan(0);
    }

    // ── #3b: out parameter ──

    [Test]
    public async Task OutParameter_CausesCompilationError()
    {
        var source = """
            using PicoAop.DI;
            using PicoAop.Abs;
            using PicoDI;
            using PicoDI.Abs;

            public interface IFoo { void Parse(out int x); }
            public sealed class Foo : IFoo
            {
                public void Parse(out int x) { x = 42; }
            }
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

        var compilationErrors = compilation
            .GetDiagnostics()
            .Where(d => d.Severity == DiagnosticSeverity.Error)
            .ToList();

        foreach (var e in compilationErrors)
            Console.WriteLine($"  error: {e.GetMessage()}");

        await Assert.That(compilationErrors.Count).IsGreaterThan(0);
    }

    // ── #6: Task return type should actually WORK (verify it's NOT broken) ──

    [Test]
    public async Task TaskReturnType_CompilesSuccessfully()
    {
        var source = """
            using System.Threading.Tasks;
            using PicoAop.DI;
            using PicoAop.Abs;
            using PicoDI;
            using PicoDI.Abs;

            public interface IFoo { Task<string> FetchAsync(); }
            public sealed class Foo : IFoo
            {
                public async Task<string> FetchAsync() => "ok";
            }
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

        var compilationErrors = compilation
            .GetDiagnostics()
            .Where(d => d.Severity == DiagnosticSeverity.Error)
            .ToList();

        foreach (var e in compilationErrors)
            Console.WriteLine($"  error: {e.GetMessage()}");

        await Assert.That(compilationErrors).IsEmpty();
    }

    // ── #8: Events are silently skipped ──

    [Test]
    public async Task Event_Interface_CausesCompilationError()
    {
        var source = """
            using System;
            using PicoAop.DI;
            using PicoAop.Abs;
            using PicoDI;
            using PicoDI.Abs;

            public interface IFoo { event EventHandler Changed; void Do(); }
            public sealed class Foo : IFoo
            {
                public event EventHandler? Changed;
                public void Do() { }
            }
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

        var compilationErrors = compilation
            .GetDiagnostics()
            .Where(d => d.Severity == DiagnosticSeverity.Error)
            .ToList();

        foreach (var e in compilationErrors)
            Console.WriteLine($"  error: {e.GetMessage()}");

        await Assert
            .That(compilationErrors.Any(e => e.GetMessage().Contains("does not implement")))
            .IsTrue();
    }

    // ── Opposite: Plain interface with only methods should succeed ──

    [Test]
    public async Task PlainInterface_OnlyMethods_CompilesSuccessfully()
    {
        var source = """
            using PicoAop.DI;
            using PicoAop.Abs;
            using PicoDI;
            using PicoDI.Abs;

            public interface IFoo { string Bar(int x); void Do(); }
            public sealed class Foo : IFoo
            {
                public string Bar(int x) => x.ToString();
                public void Do() { }
            }
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

        var compilationErrors = compilation
            .GetDiagnostics()
            .Where(d => d.Severity == DiagnosticSeverity.Error)
            .ToList();

        await Assert.That(compilationErrors).IsEmpty();
    }
}
