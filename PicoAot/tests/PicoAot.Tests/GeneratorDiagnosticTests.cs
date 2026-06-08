using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using PicoAot.Gen;

namespace PicoAot.Tests;

public class GeneratorDiagnosticTests
{
    private const string Preamble = @"
using PicoAot.Abs;

class MyInterceptor : InterceptorBase {}

interface IDummy
{
    IDummy Register<T, TImpl>() where T : class where TImpl : class;
}

static class Ext
{
    internal static IDummy InterceptBy<T>(this IDummy c) where T : class => c;
}
";

    [Test]
    public async Task SealedType_ReportsPico101()
    {
        var source = Preamble + @"
sealed class SealedSvc
{
    public void Do() {}
}

static class Reg
{
    static void X(IDummy c)
    {
        c.Register<SealedSvc, SealedSvc>().InterceptBy<MyInterceptor>();
    }
}
";
        var (diags, genTrees) = Run(source);
        Console.WriteLine($"Generated trees: {genTrees.Length}");
        foreach (var t in genTrees)
            Console.WriteLine($"  {t.FilePath}");
        foreach (var d in diags)
            Console.WriteLine($"DIAG: {d.Id} {d.GetMessage()}");
        var hasPico101 = diags.Any(d => d.Id == "PICO101");
        await Assert.That(hasPico101).IsTrue();
    }

    [Test]
    public async Task RefOutMethod_ReportsPico110()
    {
        var source = Preamble + @"
interface IHasRef
{
    void Do(ref int x);
}

class Impl : IHasRef
{
    public void Do(ref int x) { x = 1; }
}

static class Reg
{
    static void X(IDummy c)
    {
        c.Register<IHasRef, Impl>().InterceptBy<MyInterceptor>();
    }
}
";
        var (diags, genTrees) = Run(source);
        Console.WriteLine($"Generated trees: {genTrees.Length}");
        foreach (var t in genTrees)
            Console.WriteLine($"  {t.FilePath}");
        foreach (var d in diags)
            Console.WriteLine($"DIAG: {d.Id} {d.GetMessage()}");
        var hasPico110 = diags.Any(d => d.Id == "PICO110");
        await Assert.That(hasPico110).IsTrue();
    }

    private static (List<Diagnostic> Diagnostics, ImmutableArray<SyntaxTree> Trees) Run(string source)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(source);
        var compilation = CSharpCompilation.Create("test",
            new[] { syntaxTree },
            new[]
            {
                MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(IInterceptor).Assembly.Location),
            },
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var driver = CSharpGeneratorDriver.Create(new InterceptorGenerator());
        driver.RunGeneratorsAndUpdateCompilation(compilation, out _, out var diags);
        var result = new List<Diagnostic>(diags);
        var trees = driver.GetRunResult().GeneratedTrees;
        return (result, trees);
    }
}
