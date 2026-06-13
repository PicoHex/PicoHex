namespace PicoAop.Tests;

public abstract class GeneratorTestBase
{
    protected static async Task RunGenerator(
        string source,
        Func<GeneratorDriverRunResult, Task>? assert = null
    )
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(source);
        var compilation = CSharpCompilation.Create(
            "test",
            [syntaxTree],
            references:
            [
                MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(IInterceptor).Assembly.Location),
            ],
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
        );
        var generator = new InterceptorGenerator();
        var csharpDriver = CSharpGeneratorDriver.Create(generator);
        var driver = csharpDriver.RunGeneratorsAndUpdateCompilation(
            compilation,
            out var updatedComp,
            out var compileDiags
        );
        foreach (var d in compileDiags.Where(d => d.Severity == DiagnosticSeverity.Error))
            Console.Error.WriteLine($"COMPILE: {d.Id} {d.GetMessage()}");
        Console.Error.WriteLine($"Trees: {driver.GetRunResult().GeneratedTrees.Length}");
        Console.Error.WriteLine($"Results: {driver.GetRunResult().Results.Length}");
        if (assert is not null)
            await assert(driver.GetRunResult());
        else
        {
            var errors = compileDiags.Where(d => d.Severity == DiagnosticSeverity.Error).ToList();
            foreach (var e in errors)
                Console.WriteLine($"DIAG: {e.Id} {e.GetMessage()}");
        }
    }

    protected static string GetGeneratedOutput(GeneratorDriverRunResult result)
    {
        return string.Join("\n", result.GeneratedTrees.Select(t => t.ToString()));
    }

    protected static string RunGeneratorAndGetOutput(string source)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(source);
        var compilation = CSharpCompilation.Create(
            "test",
            new[] { syntaxTree },
            new[]
            {
                MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(IInterceptor).Assembly.Location),
            },
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
        );
        var driver = CSharpGeneratorDriver.Create(new InterceptorGenerator());
        driver.RunGeneratorsAndUpdateCompilation(compilation, out _, out _);
        var result = driver.GetRunResult();
        return string.Join("\n", result.GeneratedTrees.Select(t => t.ToString()));
    }

    protected static List<Diagnostic> RunAndGetDiagnostics(string source)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(source);
        var compilation = CSharpCompilation.Create(
            "test",
            new[] { syntaxTree },
            new[]
            {
                MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(IInterceptor).Assembly.Location),
            },
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
        );
        var driver = CSharpGeneratorDriver.Create(new InterceptorGenerator());
        driver.RunGeneratorsAndUpdateCompilation(compilation, out _, out var diagnostics);
        return [.. diagnostics];
    }
}
