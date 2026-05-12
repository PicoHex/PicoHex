namespace PicoCfg.Gen.Tests;

using System.Diagnostics.CodeAnalysis;

#pragma warning disable CS0618

public sealed class PicoCfgBindGeneratorGoldenFileTests
{
    [Test]
    public async Task PicoCfgBindGenerator_ProducesExpectedOutput()
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

        var generatedSource = await GenerateSourceAsync(inputSource);

        await VerifyAgainstGoldenFileAsync(
            "PicoCfgBindRegistrations.verified.g.cs",
            generatedSource
        );
    }

    private static async Task<string> GenerateSourceAsync(string source)
    {
        var parseOptions = CSharpParseOptions.Default.WithLanguageVersion(
            LanguageVersion.Preview
        );
        var syntaxTree = CSharpSyntaxTree.ParseText(source, parseOptions);

        var compilation = CSharpCompilation.Create(
            assemblyName: "PicoCfg.Gen.Tests.GoldenFileCompilation",
            syntaxTrees: [syntaxTree],
            references: GetMetadataReferences(),
            options: new CSharpCompilationOptions(OutputKind.ConsoleApplication)
        );

        var generator = new PicoCfgBindGenerator();
        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            [generator.AsSourceGenerator()],
            parseOptions: parseOptions
        );
        driver = driver.RunGeneratorsAndUpdateCompilation(
            compilation,
            out _,
            out _
        );

        var runResult = driver.GetRunResult();
        var generatedSources = runResult
            .Results.SelectMany(static result => result.GeneratedSources)
            .ToArray();
        var generatedRegistration = generatedSources.Single(
            sourceResult =>
                sourceResult.HintName == "PicoCfgBindRegistrations.g.cs"
        );

        return generatedRegistration.SourceText.ToString();
    }

    private static async Task VerifyAgainstGoldenFileAsync(
        string fileName,
        string actual
    )
    {
        var goldenPath = ResolveGoldenFilePath(fileName);

        if (!File.Exists(goldenPath))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(goldenPath)!);
            await File.WriteAllTextAsync(goldenPath, NormalizeLineEndings(actual));
            return;
        }

        var expected = NormalizeLineEndings(await File.ReadAllTextAsync(goldenPath));
        await Assert
            .That(NormalizeLineEndings(actual))
            .IsEqualTo(expected);
    }

    private static string NormalizeLineEndings(string text) =>
        text.Replace("\r\n", "\n").Replace("\r", "\n");

    [RequiresAssemblyFiles("Calls System.Reflection.Assembly.Location")]
    private static string ResolveGoldenFilePath(string fileName)
    {
        var assemblyDir = Path.GetDirectoryName(
            typeof(PicoCfgBindGeneratorGoldenFileTests).Assembly.Location
        )!;
        var projectDir = Path.GetFullPath(
            Path.Combine(assemblyDir, "..", "..", "..")
        );
        return Path.Combine(projectDir, "GoldenFiles", fileName);
    }

    [UnconditionalSuppressMessage(
        "AOT",
        "IL3000",
        Justification = "Roslyn-based generator tests construct metadata references from file-backed assemblies during test execution."
    )]
    private static MetadataReference[] GetMetadataReferences()
    {
        var trustedPlatformAssemblies = ((string?)
            AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES"))!.Split(
            Path.PathSeparator,
            StringSplitOptions.RemoveEmptyEntries
        );

        var explicitAssemblies = new[]
        {
            typeof(CfgBind).Assembly.Location,
        };

        return trustedPlatformAssemblies
            .Concat(explicitAssemblies)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(static path => MetadataReference.CreateFromFile(path))
            .ToArray();
    }
}

#pragma warning restore CS0618
