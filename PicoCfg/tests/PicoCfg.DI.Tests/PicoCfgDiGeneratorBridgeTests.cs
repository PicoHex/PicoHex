using PicoCfg.Gen;

namespace PicoCfg.DI.Tests;

using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using PicoCfg.Abs;
using PicoDI.Abs;

public class PicoCfgDiGeneratorBridgeTests
{
    [Test]
    public async Task RegisterCfgTransient_ClosedConcreteTarget_DoesNotProduceDiagnostics()
    {
        const string source = """
using PicoCfg;
using PicoCfg.DI;
using PicoDI;

var container = new SvcContainer();
container.RegisterCfgTransient<AppSettings>("App");

public sealed class AppSettings
{
    public string? Name { get; set; }
    public bool Enabled { get; set; }
    public int Count { get; set; }
}
""";

        var diagnostics = GetDiagnostics(source);

        await Assert
            .That(
                diagnostics.Where(
                    static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error
                )
            )
            .IsEmpty();
    }

    [Test]
    public async Task RegisterCfgTransient_OpenGenericTarget_ProducesDiagnostic()
    {
        const string source = """
using PicoCfg.DI;
using PicoDI;

public static class Entry<T>
{
    public static void Run(SvcContainer container)
        => container.RegisterCfgTransient<AppSettings<T>>("App");
}

public sealed class AppSettings<T>
{
    public T? Value { get; set; }
}
""";

        var diagnostics = GetDiagnostics(source);

        await Assert
            .That(diagnostics.Any(static diagnostic => diagnostic.Id == "PCFGGEN001"))
            .IsTrue();
    }

    private static ImmutableArray<Diagnostic> GetDiagnostics(string source)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(
            source,
            new CSharpParseOptions(LanguageVersion.Preview)
        );
        var compilation = CSharpCompilation.Create(
            assemblyName: "PicoCfg.DI.Generator.Tests",
            syntaxTrees: [syntaxTree],
            references: GetMetadataReferences(),
            options: new CSharpCompilationOptions(OutputKind.ConsoleApplication)
        );

        var generators = new ISourceGenerator[] { new PicoCfgBindGenerator().AsSourceGenerator(), };

        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            generators,
            parseOptions: new CSharpParseOptions(LanguageVersion.Preview)
        );
        driver = driver.RunGeneratorsAndUpdateCompilation(
            compilation,
            out var outputCompilation,
            out var diagnostics
        );

        return outputCompilation
            .GetDiagnostics()
            .AddRange(diagnostics)
            .AddRange(driver.GetRunResult().Diagnostics);
    }

    [System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage(
        "AOT",
        "IL3000",
        Justification = "These Roslyn-based generator tests intentionally construct metadata references from file-backed assemblies during test execution."
    )]
    private static MetadataReference[] GetMetadataReferences()
    {
        var trustedPlatformAssemblies = (
            (string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")
        )!.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries);

        var explicitAssemblies = new[]
        {
            typeof(ICfg).Assembly.Location,
            typeof(CfgBind).Assembly.Location,
            typeof(SvcDescriptor).Assembly.Location,
            typeof(PicoCfg.DI.SvcContainerExtensions).Assembly.Location,
            typeof(PicoDI.SvcContainer).Assembly.Location,
        };

        return trustedPlatformAssemblies
            .Concat(explicitAssemblies)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(static path => MetadataReference.CreateFromFile(path))
            .ToArray();
    }
}
