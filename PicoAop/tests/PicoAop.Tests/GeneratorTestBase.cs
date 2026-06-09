using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using PicoAop.Gen;

namespace PicoAop.Tests;

public abstract class GeneratorTestBase
{
    protected static Task RunGenerator(string source, Func<GeneratorDriverRunResult, Task>? assert = null)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(source);
        var compilation = CSharpCompilation.Create("test",
            [syntaxTree],
            references:
            [
                MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(IInterceptor).Assembly.Location),
            ],
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var generator = new InterceptorGenerator();
        var driver = CSharpGeneratorDriver.Create(generator);
        driver.RunGeneratorsAndUpdateCompilation(compilation, out _, out var diagnostics);

        if (assert is not null)
            assert(driver.GetRunResult());
        else
        {
            var errors = diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).ToList();
            foreach (var e in errors)
                Console.WriteLine($"DIAG: {e.Id} {e.GetMessage()}");
        }

        return Task.CompletedTask;
    }

    protected static string GetGeneratedOutput(GeneratorDriverRunResult result)
    {
        return string.Join("\n", result.GeneratedTrees.Select(t => t.ToString()));
    }
}
