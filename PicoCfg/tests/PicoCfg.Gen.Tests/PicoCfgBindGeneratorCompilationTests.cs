using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using PicoCfg;
using PicoCfg.Abs;
using PicoCfg.Gen;

namespace PicoCfg.Gen.Tests;

#pragma warning disable CS0618

public sealed class PicoCfgBindGeneratorCompilationTests
{
    [Test]
    public async Task DictionaryWithNestedInitOnly_CompilesWithoutErrors()
    {
        var result = await CompileAndGetErrorsAsync(
            """
            using System.Collections.Generic;
            using PicoCfg;
            using PicoCfg.Abs;

            public sealed class ProviderEntry
            {
                public string? Name { get; init; }
                public int Port { get; init; }
            }

            public sealed class AgentConfig
            {
                public Dictionary<string, ProviderEntry> Providers { get; set; } = new();
            }

            public static class Entry
            {
                public static AgentConfig Run(ICfg cfg) => CfgBind.Bind<AgentConfig>(cfg);
            }
            """
        );

        await AssertCompilationSucceeded(result);
    }

    [Test]
    public async Task NestedDictWithMixedInitOnlyAndSettable_CompilesWithoutErrors()
    {
        var result = await CompileAndGetErrorsAsync(
            """
            using System.Collections.Generic;
            using PicoCfg;
            using PicoCfg.Abs;

            public sealed class InnerSettings
            {
                public string? Name { get; init; }
                public bool Enabled { get; set; }
            }

            public sealed class MiddleSettings
            {
                public string? Section { get; set; }
                public Dictionary<string, InnerSettings> Items { get; set; } = new();
            }

            public sealed class RootSettings
            {
                public string? AppName { get; set; }
                public Dictionary<string, MiddleSettings> Sections { get; set; } = new();
            }

            public static class Entry
            {
                public static RootSettings Run(ICfg cfg) => CfgBind.Bind<RootSettings>(cfg);
            }
            """
        );

        await AssertCompilationSucceeded(result);
    }

    [Test]
    public async Task InitOnlyTypeWithNestedAndCollection_CompilesWithoutErrors()
    {
        var result = await CompileAndGetErrorsAsync(
            """
            using System.Collections.Generic;
            using PicoCfg;
            using PicoCfg.Abs;

            public sealed class SubConfig
            {
                public string? Key { get; init; }
                public int Timeout { get; init; }
            }

            public sealed class ComplexConfig
            {
                public string? Name { get; init; }
                public SubConfig? Sub { get; init; }
                public List<int>? Ports { get; init; }
                public Dictionary<string, string>? Metadata { get; init; }
            }

            public static class Entry
            {
                public static ComplexConfig Run(ICfg cfg) => CfgBind.Bind<ComplexConfig>(cfg);
            }
            """
        );

        await AssertCompilationSucceeded(result);
    }

    [Test]
    public async Task RecordTypeWithDictOfInitOnly_CompilesWithoutErrors()
    {
        var result = await CompileAndGetErrorsAsync(
            """
            using System.Collections.Generic;
            using PicoCfg;
            using PicoCfg.Abs;

            public sealed record ProviderEntry
            {
                public string? Name { get; init; }
                public int Port { get; init; }
            }

            public sealed record AgentConfig
            {
                public Dictionary<string, ProviderEntry> Providers { get; init; } = new();
            }

            public static class Entry
            {
                public static AgentConfig Run(ICfg cfg) => CfgBind.Bind<AgentConfig>(cfg);
            }
            """
        );

        await AssertCompilationSucceeded(result);
    }

    private static async Task AssertCompilationSucceeded(CompilationResult result)
    {
        if (result.Errors.Length > 0)
        {
            var dumpPath = Path.Combine(Path.GetTempPath(), "PicoCfgGenBug_Diagnostic.g.cs");
            await File.WriteAllTextAsync(dumpPath, result.GeneratedSource);

            var firstErrors = string.Join(Environment.NewLine, result.Errors.Take(10));
            Console.WriteLine($"Generated source dumped to: {dumpPath}");
            Console.WriteLine($"First 10 errors of {result.Errors.Length}:");
            Console.WriteLine(firstErrors);
        }

        await Assert.That(result.Errors.Length).IsEqualTo(0);
    }

    private static async Task<CompilationResult> CompileAndGetErrorsAsync(string source)
    {
        var parseOptions = CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.Preview);
        var syntaxTree = CSharpSyntaxTree.ParseText(source, parseOptions);

        var compilation = CSharpCompilation.Create(
            assemblyName: "CompilationTest",
            syntaxTrees: [syntaxTree],
            references: RoslynTestHelpers.GetMetadataReferences(),
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
        );

        var generator = new PicoCfgBindGenerator();
        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            [generator.AsSourceGenerator()],
            parseOptions: parseOptions
        );

        driver = driver.RunGeneratorsAndUpdateCompilation(
            compilation,
            out var outputCompilation,
            out var driverDiagnostics
        );

        using var ms = new MemoryStream();
        var emitResult = outputCompilation.Emit(ms);

        var allDiagnostics = ImmutableArray<Diagnostic>
            .Empty.AddRange(outputCompilation.GetDiagnostics())
            .AddRange(driverDiagnostics)
            .AddRange(emitResult.Diagnostics);

        var errors = allDiagnostics
            .Where(d => d.Severity == DiagnosticSeverity.Error)
            .Select(d => d.ToString())
            .ToArray();

        var runResult = driver.GetRunResult();
        var generatedSource = runResult
            .Results.SelectMany(r => r.GeneratedSources)
            .FirstOrDefault(s => s.HintName == "PicoCfgBindRegistrations.g.cs");

        var generatedText = generatedSource.HintName is not null
            ? generatedSource.SourceText.ToString()
            : string.Empty;

        return new CompilationResult(errors, generatedText);
    }

    private sealed record CompilationResult(string[] Errors, string GeneratedSource);
}

#pragma warning restore CS0618
