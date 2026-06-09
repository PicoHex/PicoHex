using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using PicoDI.Abs;
using PicoDI.Gen;

namespace PicoDI.Test;

public class InterceptorIntegrationTests
{
    [Test]
    public async Task PicoDiGenerator_WithInterception_DoesNotThrow()
    {
        var source =
            @"
using PicoDI.Abs;

interface ISvc { void Do(); }
class Impl : ISvc { public void Do() {} }
class MyInterceptor {}

interface IReturn
{
    IReturn InterceptBy<T>() where T : class;
}

static class Reg
{
    static void X(ISvcContainer c, IReturn r)
    {
        c.RegisterScoped<ISvc, Impl>();
        r.InterceptBy<MyInterceptor>();
    }
}
";
        var syntaxTree = CSharpSyntaxTree.ParseText(source);
        var compilation = CSharpCompilation.Create(
            "test",
            new[] { syntaxTree },
            new[]
            {
                MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(ISvcContainer).Assembly.Location),
            },
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
        );

        var generator = new ServiceRegistrationGenerator();
        var driver = CSharpGeneratorDriver.Create(generator);
        driver.RunGeneratorsAndUpdateCompilation(compilation, out _, out var diags);

        var errors = diags.Where(d => d.Severity == DiagnosticSeverity.Error).ToList();
        foreach (var e in errors)
            Console.WriteLine($"ERROR: {e.Id} {e.GetMessage()}");

        await Assert.That(errors).IsEmpty();
    }
}
