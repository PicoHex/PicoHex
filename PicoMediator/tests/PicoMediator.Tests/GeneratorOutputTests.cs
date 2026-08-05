using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using PicoMediator.Gen;

namespace PicoMediator.Tests;

// Verifies the REAL generator output — not mirrored strings. Guards the
// configurator-ordering invariant (bridge defect #2, 2026-08-04): the emitted
// handlers configurator id must Ordinal-sort BEFORE the emitted bridges
// configurator id for ANY assembly name. If the generator templates change,
// this test fails because it asserts on the generated sources themselves.
//
// Coverage map (bridge defect #2 regression suite):
//   - ordering invariant on REAL generator output  → this test
//   - user base-key subscriber + bridge coexist contract
//     → BasePublishTests.Publish_BaseTyped_NoDoubleDelivery
public sealed class GeneratorOutputTests
{
    private const string InputSource = """
        using PicoMediator.Abs;

        public record Paid(int Id) : IEvent;

        public sealed class PaidSubscriber : ISubscriber<Paid>
        {
            public ValueTask Handle(Paid e, CancellationToken ct) => ValueTask.CompletedTask;
        }

        // Base-key (unified) subscriber — must coexist with the bridge.
        public sealed class UnifiedSubscriber : ISubscriber<IEvent>
        {
            public ValueTask Handle(IEvent e, CancellationToken ct) => ValueTask.CompletedTask;
        }

        public record Ping(string M) : ICommand<string>;

        public sealed class PingHandler : ICommandHandler<Ping, string>
        {
            public ValueTask<string> Handle(Ping c, CancellationToken ct) => new("pong");
        }
        """;

    private static readonly CSharpParseOptions ParseOptions =
        CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.Preview);

    private static GeneratorDriver RunGenerator(string assemblyName)
    {
        var inputTree = CSharpSyntaxTree.ParseText(InputSource, ParseOptions);
        var compilation = CSharpCompilation.Create(
            assemblyName: assemblyName,
            syntaxTrees: [inputTree],
            references: GetMetadataReferences(),
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
        );

        var generator = new MediatorGenerator();
        return CSharpGeneratorDriver
            .Create([generator.AsSourceGenerator()], parseOptions: ParseOptions)
            .RunGeneratorsAndUpdateCompilation(compilation, out _, out _);
    }

    private static string ExtractConfiguratorId(string generatedSource, string marker)
    {
        // Marker: the quoted configurator id in the ModuleInitializer:
        //   MediatorAutoSubscriptionRegistry.Register("pico-mediator::...", ...)
        var idx = generatedSource.IndexOf(marker, StringComparison.Ordinal);
        if (idx < 0)
            throw new InvalidOperationException(
                $"Configurator marker '{marker}' not found in generated source."
            );
        var start = generatedSource.IndexOf('"', idx) + 1;
        var end = generatedSource.IndexOf('"', start);
        return generatedSource[start..end];
    }

    private static string? FindGeneratedSource(GeneratorDriver driver, string fileNamePart)
    {
        var runResult = driver.GetRunResult();
        foreach (var result in runResult.Results)
        {
            foreach (var source in result.GeneratedSources)
            {
                if (source.HintName.Contains(fileNamePart, StringComparison.Ordinal))
                    return source.SourceText.ToString();
            }
        }
        return null;
    }

    [Test]
    public async Task EmittedConfiguratorIds_HandlersAlwaysSortBeforeBridges()
    {
        // Battery of assembly names: lowercase (the old defect trigger),
        // uppercase, digit, underscore.
        foreach (var asm in new[] { "myapp", "PicoMediator.Tests", "Zapp", "123app", "_app" })
        {
            var driver = RunGenerator(asm);

            var handlersSource = FindGeneratedSource(driver, "MediatorHandlerRegistrations");
            var bridgesSource = FindGeneratedSource(driver, "MediatorEventDispatchers");
            await Assert.That(handlersSource).IsNotNull();
            await Assert.That(bridgesSource).IsNotNull();

            var handlersId = ExtractConfiguratorId(
                handlersSource!,
                "MediatorAutoSubscriptionRegistry.Register("
            );
            var bridgesId = ExtractConfiguratorId(
                bridgesSource!,
                "MediatorAutoSubscriptionRegistry.Register("
            );

            // Both ids must carry the sortable segment, and handlers must
            // sort before bridges regardless of the assembly name.
            await Assert.That(handlersId).Contains("pico-mediator::0::");
            await Assert.That(bridgesId).Contains("pico-mediator::1::");
            await Assert.That(string.CompareOrdinal(handlersId, bridgesId)).IsLessThan(0);
        }
    }

    private static MetadataReference[] GetMetadataReferences()
    {
        var trustedPlatformAssemblies = (
            (string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")
        )!.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries);

        var refs = trustedPlatformAssemblies
            .Select(static p => MetadataReference.CreateFromFile(p))
            .ToList();

        refs.Add(
            MetadataReference.CreateFromFile(typeof(PicoMediator.Abs.IEvent).Assembly.Location)
        );
        refs.Add(MetadataReference.CreateFromFile(typeof(PicoDI.Abs.ISvcScope).Assembly.Location));
        return [.. refs];
    }
}
