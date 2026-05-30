namespace PicoAop.Benchmarks;

public static class Program
{
    private static readonly BenchmarkConfig Config =
        new()
        {
            WarmupIterations = BenchmarkConfig.Default.WarmupIterations,
            SampleCount = BenchmarkConfig.Default.SampleCount,
            IterationsPerSample = BenchmarkConfig.Default.IterationsPerSample / 10,
            RetainSamples = BenchmarkConfig.Default.RetainSamples
        };

    private static readonly FormatterOptions TableOptions =
        new()
        {
            IncludePercentiles = true,
            IncludeCpuCycles = true,
            IncludeGcInfo = true
        };

    public static void Main(string[] args)
    {
        PicoBench.Runner.Initialize();

        var startTime = DateTime.UtcNow;
        var sw = Stopwatch.StartNew();

        ConsoleFormatter.WriteHeader("PicoAop Performance Benchmarks");

        // Part 1: Method Call Throughput
        var voidSuite = BenchmarkRunner.Run<MethodCallVoidBenchmarks>(Config);
        var returnSuite = BenchmarkRunner.Run<MethodCallReturnBenchmarks>(Config);
        var taskVoidSuite = BenchmarkRunner.Run<MethodCallTaskVoidBenchmarks>(Config);
        var taskReturnSuite = BenchmarkRunner.Run<MethodCallTaskReturnBenchmarks>(Config);

        ConsoleFormatter.WriteTableWithTitle(
            "PART 1 – void Do()",
            voidSuite.Comparisons ?? [],
            TableOptions
        );
        ConsoleFormatter.WriteTableWithTitle(
            "PART 1 – T Get()",
            returnSuite.Comparisons ?? [],
            TableOptions
        );
        ConsoleFormatter.WriteTableWithTitle(
            "PART 1 – ValueTask DoAsync()",
            taskVoidSuite.Comparisons ?? [],
            TableOptions
        );
        ConsoleFormatter.WriteTableWithTitle(
            "PART 1 – ValueTask<T> GetAsync()",
            taskReturnSuite.Comparisons ?? [],
            TableOptions
        );

        // Part 2: Infrastructure Overhead
        var buildSuite = BenchmarkRunner.Run<BuildBenchmarks>(Config);
        var scopeSuite = BenchmarkRunner.Run<ScopeCreationBenchmarks>(Config);

        ConsoleFormatter.WriteTableWithTitle(
            "PART 2 – Container Build",
            buildSuite.Comparisons ?? [],
            TableOptions
        );
        ConsoleFormatter.WriteTableWithTitle(
            "PART 2 – Scope Creation",
            scopeSuite.Comparisons ?? [],
            TableOptions
        );

        // Part 3: DI Resolution Overhead
        var diSuite = BenchmarkRunner.Run<DiResolutionBenchmarks>(Config);

        ConsoleFormatter.WriteTableWithTitle(
            "PART 3 – DI Resolution (GetService<T>)",
            diSuite.Comparisons ?? [],
            TableOptions
        );

        sw.Stop();

        // Gather all comparisons
        var allComparisons = new List<ComparisonResult>();
        AddIfNotNull(allComparisons, voidSuite.Comparisons);
        AddIfNotNull(allComparisons, returnSuite.Comparisons);
        AddIfNotNull(allComparisons, taskVoidSuite.Comparisons);
        AddIfNotNull(allComparisons, taskReturnSuite.Comparisons);
        AddIfNotNull(allComparisons, buildSuite.Comparisons);
        AddIfNotNull(allComparisons, scopeSuite.Comparisons);
        AddIfNotNull(allComparisons, diSuite.Comparisons);

        // Summary
        var summaryOptions = new SummaryOptions
        {
            CandidateLabel = "PicoAop",
            GroupByCategory = true,
            ShowDetailedTable = true,
            ShowDuration = true
        };
        SummaryFormatter.Write(allComparisons, sw.Elapsed, summaryOptions);

        // Output to files
        var timestamp = startTime.ToString("yyyy-MM-dd_HH-mm-ss");
        var outputDir = Path.Combine("results", timestamp);
        Directory.CreateDirectory(outputDir);

        var fileOptions = new FormatterOptions { OutputDirectory = outputDir };

        // Markdown
        if (args.Contains("--markdown") || args.Length == 0)
        {
            var suite = new BenchmarkSuite(
                name: "PicoAop Performance Benchmarks",
                environment: new EnvironmentInfo(),
                results: allComparisons.SelectMany(c => new[] { c.Baseline, c.Candidate }).ToList(),
                duration: sw.Elapsed,
                description: "Compile-time AOP decorator chain overhead analysis",
                comparisons: allComparisons,
                timestamp: startTime
            );
            var mdPath = fileOptions.ResolvePath("benchmark-results.md");
            MarkdownFormatter.WriteToFile(mdPath, suite);
            ConsoleFormatter.WriteFileSaved("Markdown", mdPath);
        }

        // CSV
        if (args.Contains("--csv") || args.Length == 0)
        {
            var csvPath = fileOptions.ResolvePath("benchmark-results.csv");
            CsvFormatter.WriteToFile(csvPath, allComparisons);
            ConsoleFormatter.WriteFileSaved("CSV", csvPath);
        }

        // HTML
        if (args.Contains("--html") || args.Length == 0)
        {
            var suite = new BenchmarkSuite(
                name: "PicoAop Performance Benchmarks",
                environment: new EnvironmentInfo(),
                results: allComparisons.SelectMany(c => new[] { c.Baseline, c.Candidate }).ToList(),
                duration: sw.Elapsed,
                description: "Compile-time AOP decorator chain overhead analysis",
                comparisons: allComparisons,
                timestamp: startTime
            );
            var htmlPath = fileOptions.ResolvePath("benchmark-results.html");
            HtmlFormatter.WriteToFile(htmlPath, suite);
            ConsoleFormatter.WriteFileSaved("HTML", htmlPath);
        }
    }

    private static void AddIfNotNull(
        List<ComparisonResult> list,
        IReadOnlyList<ComparisonResult>? source
    )
    {
        if (source is not null)
            list.AddRange(source);
    }
}
