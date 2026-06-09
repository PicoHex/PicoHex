using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using PicoDI.Abs;
using PicoDI.Gen;

namespace PicoDI.Test;

public class InterceptorMultiChainTests
{
    [Test]
    public async Task PicoDiGen_WithMultiChain_DoesNotCrash()
    {
        var source = @"
using PicoDI.Abs;

interface ISvc { void Do(); }
class Impl : ISvc { public void Do() {} }

interface IRet { IRet InterceptBy<T>() where T : class; }

static class Reg
{
    static void X(ISvcContainer c, IRet r)
    {
        c.RegisterScoped<ISvc, Impl>();
        r.InterceptBy<MyInterceptor>();
    }
}
class MyInterceptor { }
";
        var tree = CSharpSyntaxTree.ParseText(source);
        var compilation = CSharpCompilation.Create("test",
            new[] { tree },
            new[]
            {
                MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(ISvcContainer).Assembly.Location),
            },
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var generator = new ServiceRegistrationGenerator();
        var driver = CSharpGeneratorDriver.Create(generator);
        driver.RunGeneratorsAndUpdateCompilation(compilation, out _, out var diags);

        var errors = diags.Where(d => d.Severity == DiagnosticSeverity.Error).ToList();
        foreach (var e in errors)
            Console.WriteLine($"ERROR: {e.Id} {e.GetMessage()}");
        await Assert.That(errors).IsEmpty();
    }
}
