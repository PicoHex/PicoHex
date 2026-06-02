namespace PicoDI.Test;

public sealed class PicoDISourceGeneratorTests
{
    [Test]
    public async Task ServiceRegistrationGenerator_ProducesValidOutput()
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

        driver = driver.RunGeneratorsAndUpdateCompilation(
            compilation,
            out var outputCompilation,
            out var diagnostics
        );

        using var ms = new MemoryStream();
        var result = outputCompilation.Emit(ms);
        await Assert.That(result.Success).IsTrue();

        var runResult = driver.GetRunResult();
        var generatedSources = runResult
            .Results
            .SelectMany(static r => r.GeneratedSources)
            .ToImmutableArray();

        var registrationSource = generatedSources.Single(
            s => s.HintName.Contains("ServiceRegistrations", StringComparison.Ordinal)
        );

        var generatedTree = CSharpSyntaxTree.ParseText(registrationSource.SourceText, parseOptions);

        var root = await generatedTree.GetRootAsync();
        var generatedClass = root.DescendantNodes().OfType<ClassDeclarationSyntax>().First();

        var configureMethod = generatedClass
            .DescendantNodes()
            .OfType<MethodDeclarationSyntax>()
            .Single(m => m.Identifier.Text == "ConfigureGeneratedServices");
        await Assert
            .That(
                configureMethod
                    .ParameterList
                    .Parameters[0]
                    .Modifiers
                    .Any(m => m.IsKind(SyntaxKind.ThisKeyword))
            )
            .IsTrue();

        var prebuiltField = generatedClass
            .DescendantNodes()
            .OfType<FieldDeclarationSyntax>()
            .Single(f => f.Declaration.Variables.Any(v => v.Identifier.Text == "PrebuiltCache"));
        await Assert.That(prebuiltField.Declaration.Type.ToString()).Contains("FrozenDictionary");

        var resolveClass = generatedClass
            .DescendantNodes()
            .OfType<ClassDeclarationSyntax>()
            .Single(c => c.Identifier.Text == "Resolve");
        var resolveMethod = resolveClass
            .DescendantNodes()
            .OfType<MethodDeclarationSyntax>()
            .Single(m => m.Identifier.Text == "IGoldenService");
        await Assert.That(resolveMethod.ReturnType.ToString()).Contains("IGoldenService");
    }

    [Test]
    public async Task OpenGenericOnly_DoesNotGenerateServiceRegistrations()
    {
        // PicoLog.DI scenario: only RegisterSingleton(typeof(ILogger<>), typeof(Logger<>))
        // No concrete (closed-generic) registrations. The Registration file should NOT be emitted.
        var inputSource = """
            using PicoDI;
            using PicoDI.Abs;

            public interface ILogger<T> { void Log(string msg); }
            public sealed class Logger<T> : ILogger<T> { public void Log(string msg) { } }

            public static class Setup
            {
                public static void Configure(SvcContainer container)
                {
                    container.RegisterSingleton(typeof(ILogger<>), typeof(Logger<>));
                }
            }
            """;

        var parseOptions = CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.Preview);
        var inputTree = CSharpSyntaxTree.ParseText(inputSource, parseOptions);
        var references = GetMetadataReferences();

        var compilation = CSharpCompilation.Create(
            assemblyName: "OpenGenericOnly",
            syntaxTrees: [inputTree],
            references: references,
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
        );

        var generator = new ServiceRegistrationGenerator();
        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            [generator.AsSourceGenerator()],
            parseOptions: parseOptions
        );

        driver = driver.RunGeneratorsAndUpdateCompilation(
            compilation,
            out var outputCompilation,
            out var diags
        );

        var runResult = driver.GetRunResult();
        var generatedSources = runResult
            .Results
            .SelectMany(static r => r.GeneratedSources)
            .ToImmutableArray();

        // Collect hint names for diagnostics
        var hintNames = string.Join(", ", generatedSources.Select(s => s.HintName));
        await Assert.That(hintNames).IsNotEmpty(); // ensure we have at least the metadata file

        var hasRegistration = hintNames.Contains("ServiceRegistrations", StringComparison.Ordinal);
        await Assert.That(hasRegistration).IsFalse();

        var hasMetadata = hintNames.Contains("OpenGenericMetadata", StringComparison.Ordinal);
        await Assert.That(hasMetadata).IsTrue();
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
