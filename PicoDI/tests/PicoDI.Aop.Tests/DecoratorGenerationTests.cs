using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Immutable;
using PicoDI.Gen;

namespace PicoDI.Aop.Tests;

public class DecoratorGenerationTests
{
    [Test]
    public async Task InterceptorGenerator_InterceptBy_EmitsDecoratorClass()
    {
        var inputSource = """
            using PicoDI;
            using PicoDI.Abs;
            using PicoDI.Aop;

            public interface IGreeter
            {
                string Greet(string name);
            }

            public sealed class Greeter : IGreeter
            {
                public string Greet(string name) => $"Hello, {name}";
            }

            public sealed class PassthroughInterceptor : InterceptorBase { }

            public static class Setup
            {
                public static void Configure(SvcContainer container)
                {
                    container.RegisterSingleton<PassthroughInterceptor>();
                    container.Register<IGreeter, Greeter>(SvcLifetime.Scoped)
                        .InterceptBy<PassthroughInterceptor>();
                }
            }
            """;

        var parseOptions = CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.Preview);
        var inputTree = CSharpSyntaxTree.ParseText(inputSource, parseOptions);
        var references = GetMetadataReferences();

        var compilation = CSharpCompilation.Create(
            assemblyName: "GeneratorInput",
            syntaxTrees: [inputTree],
            references: references,
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
        );

        var generator = new InterceptorGenerator();
        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            [generator.AsSourceGenerator()],
            parseOptions: parseOptions
        );

        driver = driver.RunGeneratorsAndUpdateCompilation(
            compilation,
            out var outputCompilation,
            out var diagnostics
        );

        await Assert.That(diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error)).IsEmpty();

        var runResult = driver.GetRunResult();
        var generatedSources = runResult.Results
            .SelectMany(static r => r.GeneratedSources)
            .ToImmutableArray();

        await Assert.That(generatedSources.Length).IsGreaterThan(0);
        await Assert.That(
            generatedSources.Any(s =>
                s.HintName.Contains("InterceptorRegistrations", StringComparison.Ordinal))
        ).IsTrue();

        // Verify generated output compilation emits successfully
        var emitDiags = outputCompilation.GetDiagnostics()
            .Where(d => d.Severity == DiagnosticSeverity.Error).ToList();
        await Assert.That(emitDiags).IsEmpty();

        using var ms = new MemoryStream();
        var emitResult = outputCompilation.Emit(ms);
        await Assert.That(emitResult.Success).IsTrue();
    }

    [Test]
    public async Task InterceptorGenerator_NoInterceptBy_EmitsEmptyRegistration()
    {
        var inputSource = """
            using PicoDI;
            using PicoDI.Abs;
            using PicoDI.Aop;

            public interface IGreeter
            {
                string Greet(string name);
            }

            public sealed class Greeter : IGreeter
            {
                public string Greet(string name) => $"Hello, {name}";
            }

            public static class Setup
            {
                public static void Configure(SvcContainer container)
                {
                    container.Register<IGreeter, Greeter>(SvcLifetime.Scoped);
                }
            }
            """;

        var parseOptions = CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.Preview);
        var inputTree = CSharpSyntaxTree.ParseText(inputSource, parseOptions);
        var references = GetMetadataReferences();

        var compilation = CSharpCompilation.Create(
            assemblyName: "GeneratorInput2",
            syntaxTrees: [inputTree],
            references: references,
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
        );

        var generator = new InterceptorGenerator();
        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            [generator.AsSourceGenerator()],
            parseOptions: parseOptions
        );

        driver = driver.RunGeneratorsAndUpdateCompilation(
            compilation,
            out _,
            out var diagnostics
        );

        var runResult = driver.GetRunResult();
        var generatedSources = runResult.Results
            .SelectMany(static r => r.GeneratedSources)
            .ToImmutableArray();

        await Assert.That(
            generatedSources.Any(s =>
                s.HintName.Contains("InterceptorRegistrations", StringComparison.Ordinal))
        ).IsFalse();
    }

    private static MetadataReference[] GetMetadataReferences()
    {
        var trustedPlatformAssemblies = (
            (string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")
        )!.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries);

        var explicitAssemblies = new[]
        {
            typeof(SvcContainer).Assembly.Location,
            typeof(ISvcContainer).Assembly.Location,
            typeof(IInterceptor).Assembly.Location,
            typeof(SvcContainerInterceptorExtensions).Assembly.Location,
        };

        return trustedPlatformAssemblies
            .Concat(explicitAssemblies)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(static path => MetadataReference.CreateFromFile(path))
            .ToArray();
    }
}
