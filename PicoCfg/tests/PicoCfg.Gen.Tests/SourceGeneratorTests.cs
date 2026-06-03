namespace PicoCfg.Gen.Tests;

#pragma warning disable CS0618

public sealed class PicoCfgSourceGeneratorTests
{
    [Test]
    public async Task PicoCfgBindGenerator_ProducesCompilableOutput()
    {
        var inputSource = """
            using PicoCfg;
            using PicoCfg.Abs;

            public sealed class GoldenSettings
            {
                public string? Name { get; set; }
                public int MaxRetries { get; set; } = 3;
                public bool EnableFeature { get; set; }
            }

            public static class GoldenEntry
            {
                public static GoldenSettings Bind(ICfg cfg) =>
                    CfgBind.Bind<GoldenSettings>(cfg, "App");
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

        var generator = new PicoCfgBindGenerator();
        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            [generator.AsSourceGenerator()],
            parseOptions: parseOptions
        );

        driver.RunGeneratorsAndUpdateCompilation(
            compilation,
            out var outputCompilation,
            out var diagnostics
        );

        using var ms = new MemoryStream();
        var result = outputCompilation.Emit(ms);

        var errors = result
            .Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error)
            .ToArray();

        await Assert.That(errors.Length).IsEqualTo(0);
    }

    [UnconditionalSuppressMessage(
        "AOT",
        "IL3000",
        Justification = "Roslyn-based generator tests construct metadata references from file-backed assemblies during test execution."
    )]
    private static MetadataReference[] GetMetadataReferences()
    {
        var trustedPlatformAssemblies = (
            (string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")
        )!.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries);

        var explicitAssemblies = new[] { typeof(CfgBind).Assembly.Location };

        return trustedPlatformAssemblies
            .Concat(explicitAssemblies)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(static path => MetadataReference.CreateFromFile(path))
            .ToArray();
    }
}

#pragma warning restore CS0618
