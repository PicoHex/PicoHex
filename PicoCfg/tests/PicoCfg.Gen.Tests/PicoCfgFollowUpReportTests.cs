using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using PicoCfg;
using PicoCfg.Abs;
using PicoCfg.Gen;

namespace PicoCfg.Gen.Tests;

#pragma warning disable CS0618

// BUG-REPORT 2026.8.7 follow-up: Issue A (record + IReadOnlyList member CS0103)
// and Issue C (nesting depth limit / silently empty dictionary value types).
public sealed class PicoCfgFollowUpReportTests
{
    [Test]
    public async Task PositionalRecordWithReadOnlyListMember_AsDictionaryValue_CompilesWithoutErrors()
    {
        // Issue A repro: a record (primary constructor) with an IReadOnlyList<T>
        // member used as a Dictionary<string, T> value must compile. Today the
        // generator emits a call to Bind_N that is never emitted → CS0103.
        var result = await CompileAndGetErrorsAsync(
            nameof(PositionalRecordWithReadOnlyListMember_AsDictionaryValue_CompilesWithoutErrors),
            """
            using System.Collections.Generic;
            using PicoCfg;
            using PicoCfg.Abs;

            public sealed record ModelCost(decimal Input, decimal Output);

            public sealed record ModelCostTier(string Name, decimal Price);

            public sealed record PricingRow(ModelCost Base, IReadOnlyList<ModelCostTier>? Tiers);

            public sealed class RegistryConfig
            {
                public Dictionary<string, PricingRow> Models { get; set; } = new();
            }

            public static class Entry
            {
                public static RegistryConfig Run(ICfg cfg) => CfgBind.Bind<RegistryConfig>(cfg);
            }
            """
        );

        await AssertCompilationSucceeded(
            result,
            nameof(PositionalRecordWithReadOnlyListMember_AsDictionaryValue_CompilesWithoutErrors)
        );
    }

    [Test]
    public async Task PositionalRecordAsNestedProperty_CompilesWithoutErrors()
    {
        // Same root cause as Issue A, but via a plain nested property instead of
        // a collection element: AppendNestedBindProperty also emits Bind_N.
        var result = await CompileAndGetErrorsAsync(
            nameof(PositionalRecordAsNestedProperty_CompilesWithoutErrors),
            """
            using System.Collections.Generic;
            using PicoCfg;
            using PicoCfg.Abs;

            public sealed record ModelCost(decimal Input, decimal Output);

            public sealed record PricingRow(ModelCost Base, IReadOnlyList<ModelCostTier>? Tiers);

            public sealed record ModelCostTier(string Name, decimal Price);

            public sealed class RegistryConfig
            {
                public PricingRow? Default { get; set; }
            }

            public static class Entry
            {
                public static RegistryConfig Run(ICfg cfg) => CfgBind.Bind<RegistryConfig>(cfg);
            }
            """
        );

        await AssertCompilationSucceeded(
            result,
            nameof(PositionalRecordAsNestedProperty_CompilesWithoutErrors)
        );
    }

    [Test]
    public async Task DeeplyNestedDictionaryValueTypes_CompileWithoutDiagnostics()
    {
        // Issue C: the nesting depth limit must not truncate the
        // reported shape (Dictionary<string, PricingRow> → Tiers → Cost → Input).
        var result = await CompileAndGetErrorsAsync(
            nameof(DeeplyNestedDictionaryValueTypes_CompileWithoutDiagnostics),
            """
            using System.Collections.Generic;
            using PicoCfg;
            using PicoCfg.Abs;

            public sealed class ModelCost
            {
                public decimal Input { get; set; }
                public decimal Output { get; set; }
            }

            public sealed class ModelCostTier
            {
                public string? Name { get; set; }
                public ModelCost Cost { get; set; } = new();
            }

            public sealed class PricingRow
            {
                public ModelCost Base { get; set; } = new();
                public IReadOnlyList<ModelCostTier>? Tiers { get; set; }
            }

            public sealed class RegistryConfig
            {
                public Dictionary<string, PricingRow> Models { get; set; } = new();
            }

            public static class Entry
            {
                public static RegistryConfig Run(ICfg cfg) => CfgBind.Bind<RegistryConfig>(cfg);
            }
            """
        );

        await AssertCompilationSucceeded(
            result,
            nameof(DeeplyNestedDictionaryValueTypes_CompileWithoutDiagnostics)
        );
    }

    [Test]
    public async Task SixLevelNestedPoco_CompilesWithoutDiagnostics()
    {
        // Issue C: a 6-level nested POCO chain must compile without the
        // PCFGGEN009 truncation warning (limit raised from 5 to 8).
        var result = await CompileAndGetErrorsAsync(
            nameof(SixLevelNestedPoco_CompilesWithoutDiagnostics),
            """
            using System.Collections.Generic;
            using PicoCfg;
            using PicoCfg.Abs;

            public sealed class Level6
            {
                public string? Leaf { get; set; }
            }

            public sealed class Level5
            {
                public Level6 Next { get; set; } = new();
            }

            public sealed class Level4
            {
                public Level5 Next { get; set; } = new();
            }

            public sealed class Level3
            {
                public Level4 Next { get; set; } = new();
            }

            public sealed class Level2
            {
                public Level3 Next { get; set; } = new();
            }

            public sealed class Level1
            {
                public Level2 Next { get; set; } = new();
            }

            public sealed class RootConfig
            {
                public Level1 Top { get; set; } = new();
            }

            public static class Entry
            {
                public static RootConfig Run(ICfg cfg) => CfgBind.Bind<RootConfig>(cfg);
            }
            """
        );

        await AssertCompilationSucceeded(
            result,
            nameof(SixLevelNestedPoco_CompilesWithoutDiagnostics)
        );
    }

    [Test]
    public async Task NineLevelNestedPoco_BeyondLimit_CompilesWithDiagnosticNotErrors()
    {
        // Issue C: a 9-level chain exceeds the 8-level limit. The generated code
        // must still COMPILE (no Bind_-1 CS0103) — the truncated property is
        // skipped and PCFGGEN009 is reported instead.
        var result = await CompileAndGetDiagnosticsAsync(
            nameof(NineLevelNestedPoco_BeyondLimit_CompilesWithDiagnosticNotErrors),
            """
            using System.Collections.Generic;
            using PicoCfg;
            using PicoCfg.Abs;

            public sealed class Level9
            {
                public string? Leaf { get; set; }
            }

            public sealed class Level8
            {
                public Level9 Next { get; set; } = new();
            }

            public sealed class Level7
            {
                public Level8 Next { get; set; } = new();
            }

            public sealed class Level6
            {
                public Level7 Next { get; set; } = new();
            }

            public sealed class Level5
            {
                public Level6 Next { get; set; } = new();
            }

            public sealed class Level4
            {
                public Level5 Next { get; set; } = new();
            }

            public sealed class Level3
            {
                public Level4 Next { get; set; } = new();
            }

            public sealed class Level2
            {
                public Level3 Next { get; set; } = new();
            }

            public sealed class Level1
            {
                public Level2 Next { get; set; } = new();
            }

            public sealed class RootConfig
            {
                public Level1 Top { get; set; } = new();
            }

            public static class Entry
            {
                public static RootConfig Run(ICfg cfg) => CfgBind.Bind<RootConfig>(cfg);
            }
            """
        );

        await Assert.That(result.Errors.Length).IsEqualTo(0);
        await Assert.That(result.GeneratedSource.Contains("Bind_-1")).IsFalse();
        await Assert.That(result.Warnings.Any(static w => w.Contains("PCFGGEN009"))).IsTrue();
    }

    [Test]
    public async Task NestedTypeFailingAnalysis_DoesNotEmitBindMinusOne()
    {
        // A nested type excluded by analysis (PCFGGEN002) must not produce
        // Bind_-1 references from its parent — the parent property is skipped.
        var result = await CompileAndGetDiagnosticsAsync(
            nameof(NestedTypeFailingAnalysis_DoesNotEmitBindMinusOne),
            """
            using PicoCfg;
            using PicoCfg.Abs;

            public sealed class ChildOnlyCtor
            {
                public ChildOnlyCtor(int seed) { }
                public string? Name { get; set; }
            }

            public sealed class ParentConfig
            {
                public ChildOnlyCtor Child { get; set; }
                public string? Own { get; set; }
            }

            public static class Entry
            {
                public static ChildOnlyCtor RunChild(ICfg cfg) => CfgBind.Bind<ChildOnlyCtor>(cfg);
                public static ParentConfig Run(ICfg cfg) => CfgBind.Bind<ParentConfig>(cfg);
            }
            """
        );

        await Assert.That(result.GeneratedSource.Contains("Bind_-1")).IsFalse();
        await Assert.That(result.Errors.Any(static e => e.Contains("PCFGGEN002"))).IsTrue();
    }

    private static async Task<CompilationResult> CompileAndGetDiagnosticsAsync(
        string testName,
        string source
    )
    {
        var parseOptions = CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.Preview);
        var syntaxTree = CSharpSyntaxTree.ParseText(source, parseOptions);

        var compilation = CSharpCompilation.Create(
            assemblyName: "CompilationTest",
            syntaxTrees: [syntaxTree],
            references: RoslynTestHelpers.GetMetadataReferences(),
            options: new CSharpCompilationOptions(
                OutputKind.DynamicallyLinkedLibrary,
                nullableContextOptions: NullableContextOptions.Enable
            )
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

        var warnings = allDiagnostics
            .Where(d => d.Severity == DiagnosticSeverity.Warning)
            .Select(d => d.ToString())
            .ToArray();

        var runResult = driver.GetRunResult();
        var generatedSource = runResult
            .Results.SelectMany(r => r.GeneratedSources)
            .FirstOrDefault(s => s.HintName == "PicoCfgBindRegistrations.g.cs");

        var generatedText = generatedSource.HintName is not null
            ? generatedSource.SourceText.ToString()
            : string.Empty;

        return new CompilationResult(errors, warnings, generatedText);
    }

    private static async Task AssertCompilationSucceeded(CompilationResult result, string testName)
    {
        if (result.Errors.Length > 0)
        {
            var dumpPath = Path.Combine(
                Path.GetTempPath(),
                "PicoCfgFollowUpBug_" + testName + "_Diagnostic.g.cs"
            );
            await File.WriteAllTextAsync(dumpPath, result.GeneratedSource);

            var firstErrors = string.Join(Environment.NewLine, result.Errors.Take(10));
            Console.WriteLine($"Generated source dumped to: {dumpPath}");
            Console.WriteLine($"First 10 errors of {result.Errors.Length}:");
            Console.WriteLine(firstErrors);
        }

        await Assert.That(result.Errors.Length).IsEqualTo(0);
    }

    private static async Task<CompilationResult> CompileAndGetErrorsAsync(
        string testName,
        string source
    )
    {
        var parseOptions = CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.Preview);
        var syntaxTree = CSharpSyntaxTree.ParseText(source, parseOptions);

        var compilation = CSharpCompilation.Create(
            assemblyName: "CompilationTest",
            syntaxTrees: [syntaxTree],
            references: RoslynTestHelpers.GetMetadataReferences(),
            options: new CSharpCompilationOptions(
                OutputKind.DynamicallyLinkedLibrary,
                nullableContextOptions: NullableContextOptions.Enable,
                generalDiagnosticOption: ReportDiagnostic.Error
            )
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

        var warnings = allDiagnostics
            .Where(d => d.Severity == DiagnosticSeverity.Warning)
            .Select(d => d.ToString())
            .ToArray();

        var runResult = driver.GetRunResult();
        var generatedSource = runResult
            .Results.SelectMany(r => r.GeneratedSources)
            .FirstOrDefault(s => s.HintName == "PicoCfgBindRegistrations.g.cs");

        var generatedText = generatedSource.HintName is not null
            ? generatedSource.SourceText.ToString()
            : string.Empty;

        return new CompilationResult(errors, warnings, generatedText);
    }

    private sealed record CompilationResult(
        string[] Errors,
        string[] Warnings,
        string GeneratedSource
    );
}

#pragma warning restore CS0618
