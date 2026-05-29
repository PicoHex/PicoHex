namespace PicoAop.Tests;

public abstract class GeneratorTestBase
{
    protected static (
        Compilation OutputCompilation,
        ImmutableArray<Diagnostic> Diagnostics
    ) RunGenerator(string source)
    {
        var parseOptions = CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.Preview);
        var inputTree = CSharpSyntaxTree.ParseText(source, parseOptions);
        var references = TestMetadata.GetReferences();

        var compilation = CSharpCompilation.Create(
            assemblyName: Guid.NewGuid().ToString("N"),
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

        return (outputCompilation, diagnostics);
    }
}
