if (!BenchmarkRunPlan.TryParse(args, out var runPlan, out var error))
{
    Console.Error.WriteLine(error);
    Environment.ExitCode = 1;
    return;
}

var config = new BenchmarkConfig
{
    WarmupIterations = BenchmarkConfig.Default.WarmupIterations,
    SampleCount = BenchmarkConfig.Default.SampleCount,
    IterationsPerSample = BenchmarkConfig.Default.IterationsPerSample / 10,
    RetainSamples = BenchmarkConfig.Default.RetainSamples
};

BenchmarkSuite? buildSuite = null;
BenchmarkSuite? lookupSuite = null;
if (runPlan.IncludeBaselines)
{
    buildSuite = BenchmarkRunner.Run<BuildBenchmarks>(config);
    lookupSuite = BenchmarkRunner.Run<LookupBenchmarks>(config);
}

var mixedConfig = runPlan.MixedScenario is null ? config : config;

BenchmarkSuite mixedSuite;
if (runPlan.MixedScenario is { } mixedScenario)
{
    Console.WriteLine($"Running single mixed scenario: {mixedScenario}");
    mixedSuite = BenchmarkRunner.Run(
        new MixedWorkloadSingleScenarioBenchmarks(
            mixedScenario.N,
            mixedScenario.ProviderCount,
            mixedScenario.LookupPassCount
        ),
        mixedConfig
    );
}
else
{
    mixedSuite = BenchmarkRunner.Run<MixedWorkloadBenchmarks>(mixedConfig);
}

BenchmarkSuite? reloadSuite = null;
BenchmarkSuite? reloadControlSuite = null;
if (runPlan.IncludeReload)
{
    reloadSuite = BenchmarkRunner.Run<ReloadBenchmarks>(config);
    reloadControlSuite = BenchmarkRunner.Run<ReloadControlBenchmarks>(config);
}

var formatter = new ConsoleFormatter();
if (buildSuite is not null)
    Console.WriteLine(formatter.Format(buildSuite));

if (lookupSuite is not null)
    Console.WriteLine(formatter.Format(lookupSuite));

Console.WriteLine(formatter.Format(mixedSuite));
if (reloadSuite is not null)
    Console.WriteLine(formatter.Format(reloadSuite));

if (reloadControlSuite is not null)
    Console.WriteLine(formatter.Format(reloadControlSuite));

if (buildSuite?.Comparisons is not null)
    Console.WriteLine(SummaryFormatter.Format(buildSuite.Comparisons));

if (lookupSuite?.Comparisons is not null)
    Console.WriteLine(SummaryFormatter.Format(lookupSuite.Comparisons));

if (mixedSuite.Comparisons is not null)
    Console.WriteLine(SummaryFormatter.Format(mixedSuite.Comparisons));

var outputDir = Path.Combine(AppContext.BaseDirectory, "results");
Directory.CreateDirectory(outputDir);

var mdFormatter = new MarkdownFormatter();
var sections = new List<string>();

if (buildSuite is not null)
    sections.Add(mdFormatter.Format(buildSuite));

if (lookupSuite is not null)
    sections.Add(mdFormatter.Format(lookupSuite));

sections.Add(mdFormatter.Format(mixedSuite));

if (reloadSuite is not null)
    sections.Add(mdFormatter.Format(reloadSuite));

if (reloadControlSuite is not null)
    sections.Add(mdFormatter.Format(reloadControlSuite));

var outputFileName = runPlan.MixedScenario is { } scenario
    ? $"results-mixed-n{scenario.N}-p{scenario.ProviderCount}-l{scenario.LookupPassCount}.md"
    : "results.md";

File.WriteAllText(
    Path.Combine(outputDir, outputFileName),
    string.Join(Environment.NewLine, sections)
);

Console.WriteLine($"\nResults saved to: {outputDir}");
Environment.Exit(0);
