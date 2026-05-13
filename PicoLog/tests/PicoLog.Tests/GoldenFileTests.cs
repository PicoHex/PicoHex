namespace PicoLog.Tests;

public sealed class PicoLogMessageGoldenFileTests
{
    [Test]
    public async Task PicoLogMessageGenerator_ProducesExpectedOutput()
    {
        var inputSource = """
            using PicoLog.Abs;

            public static partial class GoldenLogMessages
            {
                [PicoLogMessage(
                    LogLevel.Info,
                    EventId = 1001,
                    Message = "User {userId} logged in from {ipAddress}"
                )]
                public static partial void UserLoggedIn(
                    this ILogger logger, string userId, string ipAddress);

                [PicoLogMessage(
                    LogLevel.Warning,
                    Message = "Disk space low: {freeMb} MB remaining"
                )]
                public static partial void DiskSpaceLow(this ILogger logger, int freeMb);
            }
            """;

        var generatedSource = await GenerateSourceAsync(inputSource);

        await VerifyAgainstGoldenFileAsync(
            "PicoLogMessage.verified.g.cs",
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
            assemblyName: "PicoLog.Tests.GoldenFileCompilation",
            syntaxTrees: [syntaxTree],
            references: GetMetadataReferences(),
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
        );

        var generator = new PicoLog.Gen.PicoLogMessageGenerator();
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
        var generated = generatedSources.Single(
            sourceResult => sourceResult.HintName == "PicoLogMessage.g.cs"
        );

        return generated.SourceText.ToString();
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

        var expected = NormalizeLineEndings(
            await File.ReadAllTextAsync(goldenPath)
        );
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
            typeof(PicoLogMessageGoldenFileTests).Assembly.Location
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
            typeof(PicoLog.Abs.ILogger).Assembly.Location,
        };

        return trustedPlatformAssemblies
            .Concat(explicitAssemblies)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(static path => MetadataReference.CreateFromFile(path))
            .ToArray();
    }
}
