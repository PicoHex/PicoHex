namespace PicoAop.Tests;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using PicoAop.Abs;
using PicoAop.DI;
using PicoAop.Gen;
using PicoDI;
using PicoDI.Abs;

public class DiagnosticTests
{
    [Test]
    public async Task PICO010_InterceptByNonInterceptorType_EmitsError()
    {
        var inputSource = """
            using PicoAop.DI;
            using PicoAop.Abs;
            using PicoDI;
            using PicoDI.Abs;

            public interface IFoo { void Do(); }
            public sealed class Foo : IFoo { public void Do() { } }

            public static class Setup
            {
                public static void Configure(SvcContainer container)
                {
                    container.Register<IFoo, Foo>(SvcLifetime.Scoped)
                        .InterceptBy<string>();
                }
            }
            """;

        var parseOptions = CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.Preview);
        var inputTree = CSharpSyntaxTree.ParseText(inputSource, parseOptions);
        var references = GetMetadataReferences();

        var compilation = CSharpCompilation.Create(
            assemblyName: "DiagTest",
            syntaxTrees: [inputTree],
            references: references,
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
        );

        var generator = new InterceptorGenerator();
        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            [generator.AsSourceGenerator()],
            parseOptions: parseOptions
        );

        driver.RunGeneratorsAndUpdateCompilation(compilation, out _, out var diagnostics);

        var pico010 = diagnostics.FirstOrDefault(d => d.Id == "PICO010" && d.Severity == DiagnosticSeverity.Error);
        await Assert.That(pico010).IsNotNull();
        await Assert.That(pico010!.GetMessage()).Contains("IInterceptor");
    }

    private static MetadataReference[] GetMetadataReferences()
    {
        var trustedPlatformAssemblies = ((string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES"))!
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries);

        var explicitAssemblies = new[]
        {
            typeof(SvcContainer).Assembly.Location,
            typeof(ISvcContainer).Assembly.Location,
            typeof(IInterceptor).Assembly.Location,
            typeof(SvcContainerInterceptorExtensions).Assembly.Location,
        };

        return trustedPlatformAssemblies
            .Concat(explicitAssemblies)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(static path => MetadataReference.CreateFromFile(path))
            .ToArray();
    }
}
