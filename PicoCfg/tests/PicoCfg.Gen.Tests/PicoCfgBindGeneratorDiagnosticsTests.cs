namespace PicoCfg.Gen.Tests;

#pragma warning disable CS0618

public class PicoCfgBindGeneratorDiagnosticsTests
{
    [Test]
    public async Task UnsupportedComplexProperty_ProducesDiagnostic()
    {
        var diagnostics = await CompileAndGetDiagnosticsAsync(
            """
            using PicoCfg;
            using PicoCfg.Abs;

            public struct ComplexChild
            {
                public int Value { get; set; }
            }

            public sealed class ComplexSettings
            {
                public ComplexChild? Child { get; set; }
            }

            public static class Entry
            {
                public static ComplexSettings Run(ICfg cfg) => CfgBind.Bind<ComplexSettings>(cfg);
            }
            """
        );

        await AssertDiagnosticAsync(diagnostics, "PCFGGEN003", "ComplexSettings.Child", expectedCount: 2);
    }

    [Test]
    public async Task UnsupportedCollectionProperty_ProducesDiagnostic()
    {
        var diagnostics = await CompileAndGetDiagnosticsAsync(
            """
            using System.Collections.Generic;
            using PicoCfg;
            using PicoCfg.Abs;

            public sealed class CollectionSettings
            {
                public HashSet<int> Values { get; set; } = new();
            }

            public static class Entry
            {
                public static CollectionSettings Run(ICfg cfg) => CfgBind.Bind<CollectionSettings>(cfg);
            }
            """
        );

        await AssertDiagnosticAsync(diagnostics, "PCFGGEN004", "CollectionSettings.Values", expectedCount: 2);
    }

    [Test]
    public async Task CollectionInterfaceProperty_ProducesDiagnostic()
    {
        var diagnostics = await CompileAndGetDiagnosticsAsync(
            """
            using System.Collections.Generic;
            using PicoCfg;
            using PicoCfg.Abs;

            public sealed class InterfaceCollectionSettings
            {
                public IList<int> Values { get; set; } = new List<int>();
            }

            public static class Entry
            {
                public static InterfaceCollectionSettings Run(ICfg cfg) =>
                    CfgBind.Bind<InterfaceCollectionSettings>(cfg);
            }
            """
        );

        await AssertDiagnosticAsync(
            diagnostics,
            "PCFGGEN003",
            "InterfaceCollectionSettings.Values",
            expectedCount: 2
        );
    }

    [Test]
    public async Task MissingPublicParameterlessConstructor_ProducesDiagnostic()
    {
        var diagnostics = await CompileAndGetDiagnosticsAsync(
            """
            using PicoCfg;
            using PicoCfg.Abs;

            public sealed class NoCtorSettings
            {
                public NoCtorSettings(int value) => Value = value;
                public int Value { get; set; }
            }

            public static class Entry
            {
                public static NoCtorSettings Run(ICfg cfg) => CfgBind.Bind<NoCtorSettings>(cfg);
            }
            """
        );

        await AssertDiagnosticAsync(diagnostics, "PCFGGEN002", "NoCtorSettings");
    }

    [Test]
    public async Task OpenGenericUsage_ProducesDiagnostic()
    {
        var diagnostics = await CompileAndGetDiagnosticsAsync(
            """
            using PicoCfg;
            using PicoCfg.Abs;

            public sealed class GenericSettings<T>
            {
                public string? Name { get; set; }
            }

            public static class Entry<T>
            {
                public static GenericSettings<T> Run(ICfg cfg) => CfgBind.Bind<GenericSettings<T>>(cfg);
            }
            """
        );

        await AssertDiagnosticAsync(diagnostics, "PCFGGEN001", "GenericSettings<T>");
    }

    [Test]
    public async Task UnsupportedPropertyType_ProducesDiagnostic()
    {
        var diagnostics = await CompileAndGetDiagnosticsAsync(
            """
            using PicoCfg;
            using PicoCfg.Abs;

            public unsafe sealed class UnsupportedPropertyTypeSettings
            {
                public delegate*<void> Callback { get; set; }
            }

            public static class Entry
            {
                public static UnsupportedPropertyTypeSettings Run(ICfg cfg) => CfgBind.Bind<UnsupportedPropertyTypeSettings>(cfg);
            }
            """,
            allowUnsafe: true
        );

        await AssertDiagnosticAsync(
            diagnostics,
            "PCFGGEN005",
            "UnsupportedPropertyTypeSettings.Callback",
            expectedCount: 2
        );
    }

    [Test]
    public async Task UnsupportedPropertyShape_ProducesDiagnostic()
    {
        var diagnostics = await CompileAndGetDiagnosticsAsync(
            """
            using PicoCfg;
            using PicoCfg.Abs;

            public sealed class UnsupportedPropertyShapeSettings
            {
                public int Value { get; private set; }
            }

            public static class Entry
            {
                public static UnsupportedPropertyShapeSettings Run(ICfg cfg) => CfgBind.Bind<UnsupportedPropertyShapeSettings>(cfg);
            }
            """
        );

        await AssertDiagnosticAsync(
            diagnostics,
            "PCFGGEN006",
            "UnsupportedPropertyShapeSettings.Value",
            expectedCount: 2
        );
    }

    [Test]
    public async Task NonClassTarget_ProducesDiagnostic()
    {
        var diagnostics = await CompileAndGetDiagnosticsAsync(
            """
            using PicoCfg;
            using PicoCfg.Abs;

            public struct StructSettings
            {
                public int Value { get; set; }
            }

            public static class Entry
            {
                public static StructSettings Run(ICfg cfg) => CfgBind.Bind<StructSettings>(cfg);
            }
            """
        );

        await AssertDiagnosticAsync(diagnostics, "PCFGGEN007", "StructSettings");
    }

    [Test]
    public async Task CircularNestedTypes_ProducesCycleDiagnostic()
    {
        var diagnostics = await CompileAndGetDiagnosticsAsync(
            """
            using PicoCfg;
            using PicoCfg.Abs;

            public sealed class TypeA
            {
                public TypeB? B { get; set; }
            }

            public sealed class TypeB
            {
                public TypeA? A { get; set; }
            }

            public static class Entry
            {
                public static TypeA Run(ICfg cfg) => CfgBind.Bind<TypeA>(cfg);
            }
            """
        );

        await AssertDiagnosticAsync(
            diagnostics,
            "PCFGGEN008",
            "TypeA",
            expectedLocationKind: LocationKind.None
        );
    }

    private static async Task AssertDiagnosticAsync(
        ImmutableArray<Diagnostic> diagnostics,
        string id,
        string expectedMessageFragment,
        int expectedCount = 1,
        LocationKind expectedLocationKind = LocationKind.SourceFile
    )
    {
        var matches = diagnostics
            .Where(
                diagnostic => diagnostic.Severity == DiagnosticSeverity.Error && diagnostic.Id == id
            )
            .ToImmutableArray();

        await Assert.That(matches.Length).IsEqualTo(expectedCount);

        var match = matches[0];
        await Assert.That(match).IsNotNull();
        await Assert.That(match.Id).IsEqualTo(id);
        await Assert.That(match.Location.Kind).IsEqualTo(expectedLocationKind);
        if (expectedLocationKind == LocationKind.SourceFile)
            await Assert.That(match.Location.SourceTree).IsNotNull();
        await Assert
            .That(match.GetMessage().Contains(expectedMessageFragment, StringComparison.Ordinal))
            .IsTrue();
    }

    private static async Task<ImmutableArray<Diagnostic>> CompileAndGetDiagnosticsAsync(
        string source,
        bool allowUnsafe = false
    )
    {
        var parseOptions = CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.Preview);
        var syntaxTree = CSharpSyntaxTree.ParseText(source, parseOptions);
        var references = GetMetadataReferences();

        var compilation = CSharpCompilation.Create(
            assemblyName: "PicoCfg.Gen.Tests.DynamicCompilation",
            syntaxTrees: [syntaxTree],
            references: references,
            options: new CSharpCompilationOptions(
                OutputKind.DynamicallyLinkedLibrary,
                allowUnsafe: allowUnsafe
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
            out _
        );

        var runResult = driver.GetRunResult();
        return outputCompilation.GetDiagnostics().AddRange(runResult.Diagnostics);
    }

    [System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage(
        "AOT",
        "IL3000",
        Justification = "These Roslyn-based generator tests intentionally construct metadata references from file-backed assemblies during test execution."
    )]
    private static MetadataReference[] GetMetadataReferences()
    {
        var trustedPlatformAssemblies = (
            (string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")
        )!.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries);

        var explicitAssemblies = new[]
        {
            typeof(ICfg).Assembly.Location,
            typeof(CfgBind).Assembly.Location,
        };

        return trustedPlatformAssemblies
            .Concat(explicitAssemblies)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(static path => MetadataReference.CreateFromFile(path))
            .ToArray();
    }
}

#pragma warning restore CS0618
