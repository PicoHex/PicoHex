namespace PicoDI.Test;

using System.Diagnostics.CodeAnalysis;

public sealed class PicoDIGoldenFileTests
{
    [Test]
    public async Task ServiceRegistrationGenerator_ProducesExpectedOutput()
    {
        var inputSource = """
            using PicoDI;
            using PicoDI.Abs;

            public interface IGoldenService
            {
                string Name { get; }
            }

            public class GoldenService : IGoldenService
            {
                public string Name => "Golden";
            }

            public static class GoldenSetup
            {
                public static void Configure(SvcContainer container)
                {
                    container.RegisterSingleton<IGoldenService, GoldenService>();
                }
            }
            """;

        var generatedSources = await RunGeneratorAsync(inputSource);

        var registrationSource = generatedSources
            .Single(s => s.HintName.Contains("ServiceRegistrations", StringComparison.Ordinal))
            .SourceText.ToString();

        await VerifyAgainstGoldenFileAsync(
            "PicoDI.ServiceRegistrations.GoldenFileTest.verified.g.cs",
            registrationSource
        );
    }

    private static async Task<ImmutableArray<GeneratedSourceResult>> RunGeneratorAsync(
        string source
    )
    {
        var parseOptions = CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.Preview);
        var syntaxTree = CSharpSyntaxTree.ParseText(source, parseOptions);
        var references = GetMetadataReferences();

        var compilation = CSharpCompilation.Create(
            assemblyName: "GoldenFileTest",
            syntaxTrees: [syntaxTree],
            references: references,
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
        );

        var generator = new ServiceRegistrationGenerator();
        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            [generator.AsSourceGenerator()],
            parseOptions: parseOptions
        );
        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out _, out _);

        var runResult = driver.GetRunResult();
        return runResult
            .Results.SelectMany(static r => r.GeneratedSources)
            .ToImmutableArray();
    }

    private static async Task VerifyAgainstGoldenFileAsync(string fileName, string actual)
    {
        var goldenPath = ResolveGoldenFilePath(fileName);

        if (!File.Exists(goldenPath))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(goldenPath)!);
            await File.WriteAllTextAsync(goldenPath, NormalizeLineEndings(actual));
            return;
        }

        var expected = NormalizeLineEndings(await File.ReadAllTextAsync(goldenPath));
        await Assert.That(NormalizeLineEndings(actual)).IsEqualTo(expected);
    }

    private static string NormalizeLineEndings(string text) =>
        text.Replace("\r\n", "\n").Replace("\r", "\n");

    [RequiresAssemblyFiles("Calls System.Reflection.Assembly.Location")]
    private static string ResolveGoldenFilePath(string fileName)
    {
        var assemblyDir = Path.GetDirectoryName(
            typeof(PicoDIGoldenFileTests).Assembly.Location
        )!;
        var projectDir = Path.GetFullPath(Path.Combine(assemblyDir, "..", "..", ".."));
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
            typeof(SvcContainer).Assembly.Location,
            typeof(ISvcContainer).Assembly.Location,
        };

        return trustedPlatformAssemblies
            .Concat(explicitAssemblies)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(static path => MetadataReference.CreateFromFile(path))
            .ToArray();
    }
}
