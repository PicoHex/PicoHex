namespace PicoDI.Test;

public sealed class PicoDIGeneratorCompilationTests
{
    [Test]
    public async Task ServiceRegistrationGenerator_ProducesCompilableOutput()
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

        var parseOptions = CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.Preview);
        var inputTree = CSharpSyntaxTree.ParseText(inputSource, parseOptions);
        var references = GetMetadataReferences();

        var compilation = CSharpCompilation.Create(
            assemblyName: "GeneratorInput",
            syntaxTrees: [inputTree],
            references: references,
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
        );

        var generator = new ServiceRegistrationGenerator();
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
            .Diagnostics
            .Where(d => d.Severity == DiagnosticSeverity.Error)
            .ToArray();

        await Assert
            .That(errors.Length)
            .IsEqualTo(0, string.Join("\n", errors.Select(d => d.ToString())));
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
