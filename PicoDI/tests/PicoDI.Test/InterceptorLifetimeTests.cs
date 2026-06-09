using System.Linq;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using PicoDI.Abs;
using PicoDI.Gen;

namespace PicoDI.Test;

public sealed class InterceptorLifetimeTests
{
    private static readonly string? s_picoAopAbsPath;

    static InterceptorLifetimeTests()
    {
        var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "PicoHex.slnx")))
            dir = dir.Parent;
        if (dir is not null)
        {
            var dll = Path.Combine(
                dir.FullName,
                "PicoAop/src/PicoAop.Abs/bin/Debug/netstandard2.0/PicoAop.Abs.dll"
            );
            if (File.Exists(dll))
                s_picoAopAbsPath = dll;
        }
    }

    [UnconditionalSuppressMessage(
        "AOT",
        "IL3000",
        Justification = "Roslyn-based generator tests construct metadata references during test execution."
    )]
    private static MetadataReference[] GetMetadataReferences()
    {
        var trusted =
            ((string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES"))
            ?? throw new InvalidOperationException("TRUSTED_PLATFORM_ASSEMBLIES not set");
        var refs = trusted
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
            .Select(p => MetadataReference.CreateFromFile(p))
            .ToList();
        refs.Add(MetadataReference.CreateFromFile(typeof(ISvcContainer).Assembly.Location));
        if (s_picoAopAbsPath is not null)
            refs.Add(MetadataReference.CreateFromFile(s_picoAopAbsPath));
        return [.. refs];
    }

    private static string? GetInterceptedRegistrationLifetime(string sourceCode)
    {
        var parseOptions = CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.CSharp12);
        var syntaxTree = CSharpSyntaxTree.ParseText(sourceCode, parseOptions);
        var compilation = CSharpCompilation.Create(
            "test",
            syntaxTrees: [syntaxTree],
            references: GetMetadataReferences(),
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
        );

        var generator = new ServiceRegistrationGenerator();
        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            [generator.AsSourceGenerator()],
            parseOptions: parseOptions
        );

        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out _, out _);

        var interceptedSource = driver
            .GetRunResult()
            .Results.SelectMany(r => (IEnumerable<GeneratedSourceResult>)r.GeneratedSources)
            .Where(s => s.HintName.Contains("InterceptedRegistrations"))
            .Select(s => s.SourceText.ToString())
            .FirstOrDefault();

        if (interceptedSource is null)
            return null;

        // Extract the lifetime from generated SvcLifetime.Xxx
        var match = System.Text.RegularExpressions.Regex.Match(
            interceptedSource,
            @"SvcLifetime\.(\w+)"
        );
        return match.Success ? match.Groups[1].Value : null;
    }

    [Test]
    public async Task InterceptedSingleton_UsesSingletonLifetime()
    {
        var source = """
            using PicoDI.Abs;

            interface ISvc { void Do(); }
            class Impl : ISvc { public void Do() {} }
            class MyInterceptor {}

            interface IReg
            {
                IReg RegisterSingleton<TService, TImpl>() where TService : class where TImpl : class;
                IReg InterceptBy<TInterceptor>() where TInterceptor : class;
            }

            static class Setup
            {
                static void X(IReg r)
                {
                    r.RegisterSingleton<ISvc, Impl>().InterceptBy<MyInterceptor>();
                }
            }
            """;

        var lifetime = GetInterceptedRegistrationLifetime(source);
        await Assert.That(lifetime).IsNotNull();
        await Assert.That(lifetime).IsEqualTo("Singleton");
    }

    [Test]
    public async Task InterceptedTransient_UsesTransientLifetime()
    {
        var source = """
            using PicoDI.Abs;

            interface ISvc { void Do(); }
            class Impl : ISvc { public void Do() {} }
            class MyInterceptor {}

            interface IReg
            {
                IReg RegisterTransient<TService, TImpl>() where TService : class where TImpl : class;
                IReg InterceptBy<TInterceptor>() where TInterceptor : class;
            }

            static class Setup
            {
                static void X(IReg r)
                {
                    r.RegisterTransient<ISvc, Impl>().InterceptBy<MyInterceptor>();
                }
            }
            """;

        var lifetime = GetInterceptedRegistrationLifetime(source);
        await Assert.That(lifetime).IsNotNull();
        await Assert.That(lifetime).IsEqualTo("Transient");
    }

    [Test]
    public async Task InterceptedScoped_UsesScopedLifetime()
    {
        var source = """
            using PicoDI.Abs;

            interface ISvc { void Do(); }
            class Impl : ISvc { public void Do() {} }
            class MyInterceptor {}

            interface IReg
            {
                IReg RegisterScoped<TService, TImpl>() where TService : class where TImpl : class;
                IReg InterceptBy<TInterceptor>() where TInterceptor : class;
            }

            static class Setup
            {
                static void X(IReg r)
                {
                    r.RegisterScoped<ISvc, Impl>().InterceptBy<MyInterceptor>();
                }
            }
            """;

        var lifetime = GetInterceptedRegistrationLifetime(source);
        await Assert.That(lifetime).IsNotNull();
        await Assert.That(lifetime).IsEqualTo("Scoped");
    }

    [Test]
    public async Task InterceptedNotHardcodedScoped_WhenSingleton()
    {
        // Regression test: ensure Singleton+InterceptBy does NOT produce Scoped
        var source = """
            using PicoDI.Abs;

            interface ISvc { void Do(); }
            class Impl : ISvc { public void Do() {} }
            class MyInterceptor {}

            interface IReg
            {
                IReg RegisterSingleton<TService, TImpl>() where TService : class where TImpl : class;
                IReg InterceptBy<TInterceptor>() where TInterceptor : class;
            }

            static class Setup
            {
                static void X(IReg r)
                {
                    r.RegisterSingleton<ISvc, Impl>().InterceptBy<MyInterceptor>();
                }
            }
            """;

        var lifetime = GetInterceptedRegistrationLifetime(source);
        await Assert.That(lifetime).IsNotNull();
        await Assert.That(lifetime).IsNotEqualTo("Scoped");
    }
}
