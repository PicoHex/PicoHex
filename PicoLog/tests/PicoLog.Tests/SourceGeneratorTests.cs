namespace PicoLog.Tests;

public sealed class PicoLogSourceGeneratorTests
{
    [Test]
    public async Task PicoLogMessageGenerator_ProducesCompilableOutput()
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

        var output = await GenerateSourceAsync(inputSource);

        var parseOptions = CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.Preview);
        var outputTree = CSharpSyntaxTree.ParseText(output, parseOptions);
        var references = GetMetadataReferences();

        var verifyCompilation = CSharpCompilation.Create(
            "Verify",
            [outputTree],
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
        );

        using var ms = new MemoryStream();
        var result = verifyCompilation.Emit(ms);

        await Assert.That(result.Success).IsTrue();

        if (!result.Success)
        {
            var failures = string.Join(
                "\n",
                result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error)
            );
            Assert.Fail($"Generated code failed to compile:\n{failures}");
        }
    }

    private static async Task<string> GenerateSourceAsync(string source)
    {
        var parseOptions = CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.Preview);
        var syntaxTree = CSharpSyntaxTree.ParseText(source, parseOptions);

        var compilation = CSharpCompilation.Create(
            assemblyName: "GeneratorInput",
            syntaxTrees: [syntaxTree],
            references: GetMetadataReferences(),
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
        );

        var generator = new PicoLog.Gen.PicoLogMessageGenerator();
        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            [generator.AsSourceGenerator()],
            parseOptions: parseOptions
        );
        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out _, out _);

        var runResult = driver.GetRunResult();
        var generatedSources = runResult
            .Results
            .SelectMany(static result => result.GeneratedSources)
            .ToArray();
        var generated = generatedSources.Single(
            sourceResult => sourceResult.HintName == "PicoLogMessage.g.cs"
        );

        return generated.SourceText.ToString();
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

        var explicitAssemblies = new[] { typeof(PicoLog.Abs.ILogger).Assembly.Location, };

        return trustedPlatformAssemblies
            .Concat(explicitAssemblies)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(static path => MetadataReference.CreateFromFile(path))
            .ToArray();
    }
}
