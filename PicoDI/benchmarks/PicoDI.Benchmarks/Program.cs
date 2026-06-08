namespace PicoDI.Benchmarks;

#region Test Services

// Simple service without dependencies
public interface ISimpleService
{
    void Execute();
}

public class SimpleService : ISimpleService
{
    public void Execute() { }
}

// Service with single dependency
public interface ILogger
{
    void Log(string message);
}

public class ConsoleLogger : ILogger
{
    public void Log(string message) { }
}

public interface IServiceWithDep
{
    void Execute();
}

public class ServiceWithDep(ILogger logger) : IServiceWithDep
{
    public void Execute() => logger.Log("test");
}

// Service with multiple dependencies
public interface IRepository
{
    void Save();
}

public class Repository : IRepository
{
    public void Save() { }
}

public interface IServiceWithMultipleDeps
{
    void Execute();
}

public class ServiceWithMultipleDeps(ILogger logger, IRepository repo) : IServiceWithMultipleDeps
{
    public void Execute()
    {
        logger.Log("C");
        repo.Save();
    }
}

// Deep dependency chain (5 levels)
public interface ILevel1 { }

public interface ILevel2 { }

public interface ILevel3 { }

public interface ILevel4 { }

public interface ILevel5 { }

public class Level1 : ILevel1 { }

public class Level2(ILevel1 l1) : ILevel2
{
    public ILevel1 L1 => l1;
}

public class Level3(ILevel2 l2) : ILevel3
{
    public ILevel2 L2 => l2;
}

public class Level4(ILevel3 l3) : ILevel4
{
    public ILevel3 L3 => l3;
}

public class Level5(ILevel4 l4) : ILevel5
{
    public ILevel4 L4 => l4;
}

#endregion

#region Benchmark Categories

/// <summary>
/// Service complexity categories for benchmarking
/// </summary>
public enum ServiceComplexity
{
    NoDependency,
    SingleDependency,
    MultipleDependencies,
    DeepChain,
}

/// <summary>
/// Service lifetime for registration
/// </summary>
public enum Lifetime
{
    Transient,
    Scoped,
    Singleton,
}

#endregion

#region Static Factories

/// <summary>
/// Pre-compiled static factories - equivalent to what Source Generator produces.
/// </summary>
public static class Factories
{
    public static readonly Func<ISvcScope, ISimpleService> SimpleService =
        static _ => new SimpleService();
    public static readonly Func<ISvcScope, ILogger> Logger = static _ => new ConsoleLogger();
    public static readonly Func<ISvcScope, IServiceWithDep> ServiceWithDep =
        static s => new ServiceWithDep(s.GetService<ILogger>());
    public static readonly Func<ISvcScope, IRepository> Repository = static _ => new Repository();
    public static readonly Func<ISvcScope, IServiceWithMultipleDeps> ServiceWithMultipleDeps =
        static s => new ServiceWithMultipleDeps(
            s.GetService<ILogger>(),
            s.GetService<IRepository>()
        );
    public static readonly Func<ISvcScope, ILevel1> Level1 = static _ => new Level1();
    public static readonly Func<ISvcScope, ILevel2> Level2 = static s => new Level2(
        s.GetService<ILevel1>()
    );
    public static readonly Func<ISvcScope, ILevel3> Level3 = static s => new Level3(
        s.GetService<ILevel2>()
    );
    public static readonly Func<ISvcScope, ILevel4> Level4 = static s => new Level4(
        s.GetService<ILevel3>()
    );
    public static readonly Func<ISvcScope, ILevel5> Level5 = static s => new Level5(
        s.GetService<ILevel4>()
    );
}

#endregion

#region Container Setup

public static class ContainerSetup
{
    public static SvcContainer CreatePicoContainer(ServiceComplexity complexity, Lifetime lifetime)
    {
        var container = new SvcContainer(autoConfigureFromGenerator: false);
        var svcLifetime = ToSvcLifetime(lifetime);

        switch (complexity)
        {
            case ServiceComplexity.NoDependency:
                container.Register(
                    new SvcDescriptor(typeof(ISimpleService), Factories.SimpleService, svcLifetime)
                );
                break;
            case ServiceComplexity.SingleDependency:
                container.Register(
                    new SvcDescriptor(typeof(ILogger), Factories.Logger, svcLifetime)
                );
                container.Register(
                    new SvcDescriptor(
                        typeof(IServiceWithDep),
                        Factories.ServiceWithDep,
                        svcLifetime
                    )
                );
                break;
            case ServiceComplexity.MultipleDependencies:
                container.Register(
                    new SvcDescriptor(typeof(ILogger), Factories.Logger, svcLifetime)
                );
                container.Register(
                    new SvcDescriptor(typeof(IRepository), Factories.Repository, svcLifetime)
                );
                container.Register(
                    new SvcDescriptor(
                        typeof(IServiceWithMultipleDeps),
                        Factories.ServiceWithMultipleDeps,
                        svcLifetime
                    )
                );
                break;
            case ServiceComplexity.DeepChain:
                container.Register(
                    new SvcDescriptor(typeof(ILevel1), Factories.Level1, svcLifetime)
                );
                container.Register(
                    new SvcDescriptor(typeof(ILevel2), Factories.Level2, svcLifetime)
                );
                container.Register(
                    new SvcDescriptor(typeof(ILevel3), Factories.Level3, svcLifetime)
                );
                container.Register(
                    new SvcDescriptor(typeof(ILevel4), Factories.Level4, svcLifetime)
                );
                container.Register(
                    new SvcDescriptor(typeof(ILevel5), Factories.Level5, svcLifetime)
                );
                break;
        }

        container.Build();
        return container;
    }

    public static ServiceProvider CreateMsContainer(ServiceComplexity complexity, Lifetime lifetime)
    {
        var services = new ServiceCollection();

        switch (complexity)
        {
            case ServiceComplexity.NoDependency:
                RegisterMs<ISimpleService>(services, lifetime, _ => new SimpleService());
                break;
            case ServiceComplexity.SingleDependency:
                RegisterMs<ILogger>(services, lifetime, _ => new ConsoleLogger());
                RegisterMs<IServiceWithDep>(
                    services,
                    lifetime,
                    sp => new ServiceWithDep(sp.GetRequiredService<ILogger>())
                );
                break;
            case ServiceComplexity.MultipleDependencies:
                RegisterMs<ILogger>(services, lifetime, _ => new ConsoleLogger());
                RegisterMs<IRepository>(services, lifetime, _ => new Repository());
                RegisterMs<IServiceWithMultipleDeps>(
                    services,
                    lifetime,
                    sp => new ServiceWithMultipleDeps(
                        sp.GetRequiredService<ILogger>(),
                        sp.GetRequiredService<IRepository>()
                    )
                );
                break;
            case ServiceComplexity.DeepChain:
                RegisterMs<ILevel1>(services, lifetime, _ => new Level1());
                RegisterMs<ILevel2>(
                    services,
                    lifetime,
                    sp => new Level2(sp.GetRequiredService<ILevel1>())
                );
                RegisterMs<ILevel3>(
                    services,
                    lifetime,
                    sp => new Level3(sp.GetRequiredService<ILevel2>())
                );
                RegisterMs<ILevel4>(
                    services,
                    lifetime,
                    sp => new Level4(sp.GetRequiredService<ILevel3>())
                );
                RegisterMs<ILevel5>(
                    services,
                    lifetime,
                    sp => new Level5(sp.GetRequiredService<ILevel4>())
                );
                break;
        }

        return services.BuildServiceProvider();
    }

    private static void RegisterMs<T>(
        ServiceCollection services,
        Lifetime lifetime,
        Func<IServiceProvider, T> factory
    )
        where T : class
    {
        switch (lifetime)
        {
            case Lifetime.Transient:
                services.AddTransient(factory);
                break;
            case Lifetime.Scoped:
                services.AddScoped(factory);
                break;
            case Lifetime.Singleton:
                services.AddSingleton(factory);
                break;
        }
    }

    private static SvcLifetime ToSvcLifetime(Lifetime lifetime) =>
        lifetime switch
        {
            Lifetime.Transient => SvcLifetime.Transient,
            Lifetime.Scoped => SvcLifetime.Scoped,
            Lifetime.Singleton => SvcLifetime.Singleton,
            _ => throw new ArgumentOutOfRangeException(nameof(lifetime)),
        };
}

#endregion

#region Benchmark Helpers

/// <summary>
/// Wraps an ISvcScope to implement IDisposable, enabling PicoBench's RunScoped&lt;TScope&gt;
/// to work with PicoDI's IAsyncDisposable-only scopes. Dispose is a no-op in benchmark code
/// to avoid async overhead — benchmark services are short-lived and resource-free.
/// </summary>
internal sealed class ScopeWrapper : IDisposable
{
    public ISvcScope Scope { get; }

    public ScopeWrapper(ISvcScope scope) => Scope = scope;

    public void Dispose() { }
}

#endregion

#region Main Program

public static class Program
{
    private static readonly BenchmarkConfig Config = BenchmarkConfig.Default;

    private static readonly FormatterOptions TableOptions = new()
    {
        IncludePercentiles = true,
        IncludeCpuCycles = true,
        IncludeGcInfo = true,
    };

    public static void Main(string[] args)
    {
        PicoBench.Runner.Initialize();

        var env = CreateEnvironmentInfo();
        var startTime = DateTime.UtcNow;
        var sw = Stopwatch.StartNew();

        // Header
        ConsoleFormatter.WriteHeader("PicoDI vs Microsoft.DI - Comprehensive Benchmark");
        ConsoleFormatter.WriteEnvironment(env, Config);

        var complexities = new[]
        {
            ServiceComplexity.NoDependency,
            ServiceComplexity.SingleDependency,
            ServiceComplexity.MultipleDependencies,
            ServiceComplexity.DeepChain,
        };
        var lifetimes = new[] { Lifetime.Transient, Lifetime.Scoped, Lifetime.Singleton };

        var comparisons = new List<ComparisonResult>();

        // Part 1: Service Resolution by Complexity
        var part1Comparisons = new List<ComparisonResult>();
        foreach (var complexity in complexities)
        {
            foreach (var lifetime in lifetimes)
            {
                var comparison = RunResolutionBenchmark(complexity, lifetime);
                comparisons.Add(comparison);
                part1Comparisons.Add(comparison);
            }
        }

        ConsoleFormatter.WriteTableWithTitle(
            "PART 1: Service Resolution by Complexity (GetService<T>() within a scope)",
            part1Comparisons,
            TableOptions
        );

        // Part 2: Infrastructure Overhead
        var part2Comparisons = new List<ComparisonResult>();

        var containerSetup = RunContainerSetupBenchmark();
        comparisons.Add(containerSetup);
        part2Comparisons.Add(containerSetup);

        var scopeCreation = RunScopeCreationBenchmark();
        comparisons.Add(scopeCreation);
        part2Comparisons.Add(scopeCreation);

        ConsoleFormatter.WriteTableWithTitle(
            "PART 2: Infrastructure Overhead",
            part2Comparisons,
            TableOptions
        );

        // Part 3: Resolution Scenarios
        var part3Comparisons = new List<ComparisonResult>();

        foreach (var lifetime in lifetimes)
        {
            var single = RunSingleResolutionBenchmark(lifetime);
            comparisons.Add(single);
            part3Comparisons.Add(single);
        }

        foreach (var lifetime in lifetimes)
        {
            var multi = RunMultipleResolutionsBenchmark(lifetime);
            comparisons.Add(multi);
            part3Comparisons.Add(multi);
        }

        ConsoleFormatter.WriteTableWithTitle(
            "PART 3: Resolution Scenarios (hot path performance)",
            part3Comparisons,
            TableOptions
        );

        sw.Stop();

        // Summary
        var summaryOptions = new SummaryOptions
        {
            CandidateLabel = "PicoDI",
            GroupByCategory = true,
            ShowDetailedTable = true,
            ShowDuration = true,
        };
        SummaryFormatter.Write(comparisons, sw.Elapsed, summaryOptions);

        // Output to files
        var timestamp = startTime.ToString("yyyy-MM-dd_HH-mm-ss");
        var outputDir = Path.Combine("results", timestamp);
        var fileOptions = new FormatterOptions { OutputDirectory = outputDir };

        if (args.Contains("--csv") || args.Contains("--all"))
        {
            var csvPath = fileOptions.ResolvePath("benchmark-results.csv");
            CsvFormatter.WriteToFile(csvPath, comparisons);
            ConsoleFormatter.WriteFileSaved("CSV", csvPath);
        }

        var suite = new BenchmarkSuite(
            name: "PicoDI vs Microsoft.DI Benchmark",
            environment: env,
            results: comparisons.SelectMany(c => new[] { c.Baseline, c.Candidate }).ToList(),
            duration: sw.Elapsed,
            description: "Comprehensive DI container performance comparison",
            comparisons: comparisons,
            timestamp: startTime
        );

        if (args.Contains("--markdown") || args.Contains("--all"))
        {
            var mdPath = fileOptions.ResolvePath("benchmark-results.md");
            MarkdownFormatter.WriteToFile(mdPath, suite);
            ConsoleFormatter.WriteFileSaved("Markdown", mdPath);
        }

        if (args.Contains("--html") || args.Contains("--all"))
        {
            var htmlPath = fileOptions.ResolvePath("benchmark-results.html");
            HtmlFormatter.WriteToFile(htmlPath, suite);
            ConsoleFormatter.WriteFileSaved("HTML", htmlPath);
        }
    }

    private static EnvironmentInfo CreateEnvironmentInfo() =>
        new() { ExecutionMode = GetExecutionMode() };

    private static RuntimeExecutionMode GetExecutionMode()
    {
#if PICO_DI_BENCHMARKS_NATIVE_AOT
        return RuntimeExecutionMode.NativeAot;
#else
        return RuntimeExecutionMode.Unknown;
#endif
    }

    #region Benchmark Methods

    private static ComparisonResult RunResolutionBenchmark(
        ServiceComplexity complexity,
        Lifetime lifetime
    )
    {
        var name = $"{complexity} × {lifetime}";

        var picoContainer = ContainerSetup.CreatePicoContainer(complexity, lifetime);
        using var msProvider = ContainerSetup.CreateMsContainer(complexity, lifetime);

        var (picoResolve, msResolve) = GetResolveActions(complexity);

        var picoResult = Benchmark.RunScoped(
            $"Pico/{name}",
            () => new ScopeWrapper(picoContainer.CreateScope()),
            wrapper => picoResolve(wrapper.Scope),
            Config
        );

        var msResult = Benchmark.RunScoped(
            $"MsDI/{name}",
            () => msProvider.CreateScope(),
            scope => msResolve(((IServiceScope)scope).ServiceProvider),
            Config
        );

        var result = new ComparisonResult(
            name: name,
            baseline: msResult,
            candidate: picoResult,
            category: complexity.ToString(),
            tags: new Dictionary<string, string>
            {
                ["Complexity"] = complexity.ToString(),
                ["Lifetime"] = lifetime.ToString(),
            }
        );

        return result;
    }

    private static ComparisonResult RunContainerSetupBenchmark()
    {
        var config = new BenchmarkConfig
        {
            WarmupIterations = Config.WarmupIterations,
            SampleCount = Config.SampleCount,
            IterationsPerSample = Config.IterationsPerSample / 10,
            RetainSamples = Config.RetainSamples,
        };

        var picoResult = Benchmark.Run(
            "Pico/ContainerSetup",
            () =>
            {
                var c = ContainerSetup.CreatePicoContainer(
                    ServiceComplexity.SingleDependency,
                    Lifetime.Scoped
                );
            },
            config
        );

        var msResult = Benchmark.Run(
            "MsDI/ContainerSetup",
            () =>
            {
                using var p = ContainerSetup.CreateMsContainer(
                    ServiceComplexity.SingleDependency,
                    Lifetime.Scoped
                );
            },
            config
        );

        return new ComparisonResult(
            name: "ContainerSetup",
            baseline: msResult,
            candidate: picoResult,
            category: "Infrastructure"
        );
    }

    private static ComparisonResult RunScopeCreationBenchmark()
    {
        var picoContainer = ContainerSetup.CreatePicoContainer(
            ServiceComplexity.SingleDependency,
            Lifetime.Scoped
        );
        using var msProvider = ContainerSetup.CreateMsContainer(
            ServiceComplexity.SingleDependency,
            Lifetime.Scoped
        );

        var picoResult = Benchmark.Run(
            "Pico/ScopeCreation",
            () =>
            {
                var scope = picoContainer.CreateScope();
            },
            Config
        );

        var msResult = Benchmark.Run(
            "MsDI/ScopeCreation",
            () =>
            {
                using var scope = msProvider.CreateScope();
            },
            Config
        );

        var result = new ComparisonResult(
            name: "ScopeCreation",
            baseline: msResult,
            candidate: picoResult,
            category: "Infrastructure"
        );

        return result;
    }

    private static ComparisonResult RunSingleResolutionBenchmark(Lifetime lifetime)
    {
        var name = $"SingleResolution × {lifetime}";

        var picoContainer = ContainerSetup.CreatePicoContainer(
            ServiceComplexity.SingleDependency,
            lifetime
        );
        using var msProvider = ContainerSetup.CreateMsContainer(
            ServiceComplexity.SingleDependency,
            lifetime
        );

        var picoResult = Benchmark.RunScoped(
            $"Pico/{name}",
            () => new ScopeWrapper(picoContainer.CreateScope()),
            wrapper => _ = wrapper.Scope.GetService<IServiceWithDep>(),
            Config
        );

        var msResult = Benchmark.RunScoped(
            $"MsDI/{name}",
            () => msProvider.CreateScope(),
            scope =>
                _ = ((IServiceScope)scope).ServiceProvider.GetRequiredService<IServiceWithDep>(),
            Config
        );

        var result = new ComparisonResult(
            name: name,
            baseline: msResult,
            candidate: picoResult,
            category: "Resolution",
            tags: new Dictionary<string, string>
            {
                ["Scenario"] = "SingleResolution",
                ["Lifetime"] = lifetime.ToString(),
            }
        );

        return result;
    }

    private static ComparisonResult RunMultipleResolutionsBenchmark(Lifetime lifetime)
    {
        var name = $"MultipleResolutions × {lifetime}";
        const int innerLoop = 100;

        var picoContainer = ContainerSetup.CreatePicoContainer(
            ServiceComplexity.SingleDependency,
            lifetime
        );
        using var msProvider = ContainerSetup.CreateMsContainer(
            ServiceComplexity.SingleDependency,
            lifetime
        );

        var picoResult = Benchmark.RunScoped(
            $"Pico/{name}",
            () => new ScopeWrapper(picoContainer.CreateScope()),
            wrapper =>
            {
                for (int i = 0; i < innerLoop; i++)
                    _ = wrapper.Scope.GetService<IServiceWithDep>();
            },
            Config
        );

        var msResult = Benchmark.RunScoped(
            $"MsDI/{name}",
            () => msProvider.CreateScope(),
            scope =>
            {
                var sp = ((IServiceScope)scope).ServiceProvider;
                for (int i = 0; i < innerLoop; i++)
                    _ = sp.GetRequiredService<IServiceWithDep>();
            },
            Config
        );

        var result = new ComparisonResult(
            name: name,
            baseline: msResult,
            candidate: picoResult,
            category: "Resolution",
            tags: new Dictionary<string, string>
            {
                ["Scenario"] = "MultipleResolutions",
                ["Lifetime"] = lifetime.ToString(),
            }
        );

        return result;
    }

    private static (Action<ISvcScope> pico, Action<IServiceProvider> ms) GetResolveActions(
        ServiceComplexity complexity
    )
    {
        return complexity switch
        {
            ServiceComplexity.NoDependency => (
                static s => s.GetService<ISimpleService>(),
                static s => s.GetRequiredService<ISimpleService>()
            ),
            ServiceComplexity.SingleDependency => (
                static s => s.GetService<IServiceWithDep>(),
                static s => s.GetRequiredService<IServiceWithDep>()
            ),
            ServiceComplexity.MultipleDependencies => (
                static s => s.GetService<IServiceWithMultipleDeps>(),
                static s => s.GetRequiredService<IServiceWithMultipleDeps>()
            ),
            ServiceComplexity.DeepChain => (
                static s => s.GetService<ILevel5>(),
                static s => s.GetRequiredService<ILevel5>()
            ),
            _ => throw new ArgumentOutOfRangeException(nameof(complexity)),
        };
    }

    #endregion
}

#endregion
