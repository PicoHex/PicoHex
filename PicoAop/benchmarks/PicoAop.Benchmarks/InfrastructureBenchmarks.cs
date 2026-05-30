namespace PicoAop.Benchmarks;

public static class InfrastructureContainers
{
    /// <summary>
    /// Container with interceptors registered but NO InterceptBy chains (decorator-free).
    /// autoConfigureFromGenerator is false to skip PicoAop.Gen decorator registrations.
    /// All registrations use factory delegates since type-based registration requires
    /// PicoDI.Gen source generator output (also skipped).
    /// </summary>
    public static SvcContainer CreateBaseline()
    {
        var container = new SvcContainer(autoConfigureFromGenerator: false);
        container.RegisterSingleton<N1>(static _ => new N1());
        container.RegisterSingleton<N2>(static _ => new N2());
        container.RegisterSingleton<N3>(static _ => new N3());
        container.RegisterSingleton<N4>(static _ => new N4());
        container.RegisterSingleton<N5>(static _ => new N5());
        // Register raw services with factory delegates — no InterceptBy
        container.Register<IVoidSvc_D0>(static _ => new VoidSvc(), SvcLifetime.Scoped);
        container.Register<IReturnSvc_D0>(static _ => new ReturnSvc(), SvcLifetime.Scoped);
        container.Register<ITaskVoidSvc_D0>(static _ => new TaskVoidSvc(), SvcLifetime.Scoped);
        container.Register<ITaskReturnSvc_D0>(static _ => new TaskReturnSvc(), SvcLifetime.Scoped);
        container.Build();
        return container;
    }
}

[BenchmarkClass(Description = "Infrastructure overhead: Build")]
public partial class BuildBenchmarks
{
    [Benchmark(Baseline = true, Description = "Build (no decorators)")]
    public void Baseline()
    {
        var c = InfrastructureContainers.CreateBaseline();
    }

    [Benchmark(Description = "Build (all decorators)")]
    public void Full()
    {
        var c = ContainerFactory.Create();
    }
}

[BenchmarkClass(Description = "Infrastructure overhead: CreateScope")]
public partial class ScopeCreationBenchmarks
{
    private SvcContainer _baselineContainer = null!;
    private SvcContainer _fullContainer = null!;

    [GlobalSetup]
    public void Setup()
    {
        _baselineContainer = InfrastructureContainers.CreateBaseline();
        _fullContainer = ContainerFactory.Create();
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _baselineContainer.DisposeAsync().AsTask().GetAwaiter().GetResult();
        _fullContainer.DisposeAsync().AsTask().GetAwaiter().GetResult();
    }

    [Benchmark(Baseline = true, Description = "CreateScope (no decorators)")]
    public void Baseline()
    {
        var scope = _baselineContainer.CreateScope();
    }

    [Benchmark(Description = "CreateScope (all decorators)")]
    public void Full()
    {
        var scope = _fullContainer.CreateScope();
    }
}
