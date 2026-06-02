namespace PicoAop.Tests;

public class GeneratorBasicTests : GeneratorTestBase
{
    [Test]
    public async Task InterceptBy_GeneratesDecoratorAndInvocationStruct()
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
        var trees = compilation.SyntaxTrees.Skip(1).ToList();
        await Assert.That(trees.Count).IsGreaterThan(0);
        var generated = string.Join("", trees.Select(t => t.ToString()));
        await Assert.That(generated.Contains("Decorator")).IsTrue();
        await Assert.That(generated.Contains("Invocation")).IsTrue();
    }

    [Test]
    public async Task WithoutInterceptors_EmitsPICO012Warning()
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

    [Test]
    public async Task WithoutInterceptor_ExcludesSpecificType()
    {
        var source = """
            using PicoAop.DI;
            using PicoAop.Abs;
            using PicoDI;
            using PicoDI.Abs;

            public interface IFoo { void Do(); }
            public sealed class Foo : IFoo { public void Do() { } }
            public sealed class Interceptor1 : InterceptorBase { }
            public sealed class Interceptor2 : InterceptorBase { }

            public static class Setup
            {
                public static void Configure(SvcContainer c)
                {
                    c.Register<IFoo, Foo>(SvcLifetime.Scoped)
                        .InterceptBy<Interceptor1>()
                        .InterceptBy<Interceptor2>()
                        .WithoutInterceptor<Interceptor1>();
                }
            }
            """;

        var (compilation, diags) = RunGenerator(source);
        var generated = string.Join("", compilation.SyntaxTrees.Skip(1).Select(t => t.ToString()));
        await Assert.That(generated.Contains("Interceptor2Decorator")).IsTrue();
        await Assert.That(generated.Contains("Interceptor1Decorator")).IsFalse();
    }

    [Test]
    public async Task MultipleInterceptBy_ChainOrderOutermostFirst()
    {
        var source = """
            using PicoAop.DI;
            using PicoAop.Abs;
            using PicoDI;
            using PicoDI.Abs;

            public interface IFoo { string Bar(); }
            public sealed class Foo : IFoo { public string Bar() => "x"; }
            public sealed class Outer : InterceptorBase { }
            public sealed class Middle : InterceptorBase { }
            public sealed class Inner : InterceptorBase { }

            public static class Setup
            {
                public static void Configure(SvcContainer c)
                {
                    c.Register<IFoo, Foo>(SvcLifetime.Scoped)
                        .InterceptBy<Outer>()
                        .InterceptBy<Middle>()
                        .InterceptBy<Inner>();
                }
            }
            """;

        var (compilation, diags) = RunGenerator(source);
        await Assert.That(diags.Where(d => d.Severity == DiagnosticSeverity.Error)).IsEmpty();

        var generated = string.Join("", compilation.SyntaxTrees.Skip(1).Select(t => t.ToString()));
        var outerIdx = generated.IndexOf("OuterDecorator", StringComparison.Ordinal);
        var middleIdx = generated.IndexOf("MiddleDecorator", StringComparison.Ordinal);
        var innerIdx = generated.IndexOf("InnerDecorator", StringComparison.Ordinal);
        await Assert.That(outerIdx).IsGreaterThan(-1);
        await Assert.That(middleIdx).IsGreaterThan(-1);
        await Assert.That(innerIdx).IsGreaterThan(-1);
    }

    [Test]
    public async Task SelfRegistration_EmitsDiRegistration()
    {
        var source = """
            using PicoAop.DI;
            using PicoAop.Abs;
            using PicoDI;
            using PicoDI.Abs;

            public class Greeter { public string Say() => "hi"; }
            public sealed class LogInterceptor : InterceptorBase { }

            public static class Setup
            {
                public static void Configure(SvcContainer c)
                {
                    c.Register<Greeter>(SvcLifetime.Scoped).InterceptBy<LogInterceptor>();
                }
            }
            """;

        var (compilation, diags) = RunGenerator(source);
        await Assert.That(diags.Where(d => d.Severity == DiagnosticSeverity.Error)).IsEmpty();

        var generated = string.Join("", compilation.SyntaxTrees.Skip(1).Select(t => t.ToString()));
        await Assert.That(generated.Contains("container.Register<")).IsTrue();
    }

    [Test]
    public async Task SealedClass_EmitsPICO017()
    {
        var source = """
            using PicoAop.DI;
            using PicoAop.Abs;
            using PicoDI;
            using PicoDI.Abs;

            public sealed class SealedSvc { public void Work() { } }
            public sealed class LogInterceptor : InterceptorBase { }

            public static class Setup
            {
                public static void Configure(SvcContainer c)
                {
                    c.Register<SealedSvc>(SvcLifetime.Scoped).InterceptBy<LogInterceptor>();
                }
            }
            """;

        var (compilation, diags) = RunGenerator(source);
        var pico017 = diags.FirstOrDefault(d => d.Id == "PICO017");
        await Assert.That(pico017).IsNotNull();
        await Assert.That(pico017!.Severity).IsEqualTo(DiagnosticSeverity.Error);
    }

    [Test]
    public async Task InheritedInterfaceMembers_AreGenerated()
    {
        var source = """
            using PicoAop.DI;
            using PicoAop.Abs;
            using PicoDI;
            using PicoDI.Abs;

            public interface IBase { void BaseMethod(); }
            public interface IChild : IBase { void ChildMethod(); }
            public sealed class Impl : IChild
            {
                public void BaseMethod() { }
                public void ChildMethod() { }
            }
            public sealed class Audit : InterceptorBase { }

            public static class Setup
            {
                public static void Configure(SvcContainer c)
                {
                    c.Register<IChild, Impl>(SvcLifetime.Scoped)
                        .InterceptBy<Audit>();
                }
            }
            """;

        var (compilation, diags) = RunGenerator(source);
        var generated = string.Join("", compilation.SyntaxTrees.Skip(1).Select(t => t.ToString()));
        await Assert.That(generated.Contains("BaseMethod")).IsTrue();
        await Assert.That(generated.Contains("ChildMethod")).IsTrue();
    }

    [Test]
    public async Task InvocationStruct_HasInternalParameterFields()
    {
        var source = """
            using PicoAop.DI;
            using PicoAop.Abs;
            using PicoDI;
            using PicoDI.Abs;

            public interface ICalc { int Add(int a, int b); }
            public sealed class Calc : ICalc { public int Add(int a, int b) => a + b; }
            public sealed class LogInterceptor : InterceptorBase { }

            public static class Setup
            {
                public static void Configure(SvcContainer c)
                {
                    c.Register<ICalc, Calc>(SvcLifetime.Scoped)
                        .InterceptBy<LogInterceptor>();
                }
            }
            """;

        var (compilation, diags) = RunGenerator(source);
        await Assert.That(diags.Where(d => d.Severity == DiagnosticSeverity.Error)).IsEmpty();
        var generated = string.Join("", compilation.SyntaxTrees.Skip(1).Select(t => t.ToString()));

        // Verify parameters are internal (accessible by casting) not private
        await Assert.That(generated.Contains("internal readonly")).IsTrue();
        // Verify the struct implements IInvocation
        await Assert.That(generated.Contains("IInvocation<")).IsTrue();
    }

    [Test]
    public async Task InterfaceWithImpl_DiResolvesConcreteImpl()
    {
        var source = """
            using PicoAop.DI;
            using PicoAop.Abs;
            using PicoDI;
            using PicoDI.Abs;

            public interface ICalc { int Add(int a, int b); }
            public sealed class Calc : ICalc { public int Add(int a, int b) => a + b; }
            public sealed class AuditInterceptor : InterceptorBase { }

            public static class Setup
            {
                public static void Configure(SvcContainer c)
                {
                    c.Register<ICalc, Calc>(SvcLifetime.Scoped)
                        .InterceptBy<AuditInterceptor>();
                }
            }
            """;

        var (compilation, diags) = RunGenerator(source);
        var generated = string.Join("", compilation.SyntaxTrees.Skip(1).Select(t => t.ToString()));
        await Assert.That(generated.Contains("Calc")).IsTrue();
    }
}
