namespace PicoAop.Benchmarks;

[BenchmarkClass(Description = "DI resolution: GetService<T>() for decorated services")]
public partial class DiResolutionBenchmarks
{
    private SvcContainer _container = null!;

    [GlobalSetup]
    public void Setup()
    {
        _container = ContainerFactory.Create();
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _container.DisposeAsync().AsTask().GetAwaiter().GetResult();
    }

    // Note: scopes are not disposed in benchmark iterations — services are simple
    // (no unmanaged resources) so GC cleanup is sufficient and avoids async overhead.

    [Benchmark(Baseline = true, Description = "Resolve IVoidSvc D0 (raw)")]
    public void Void_D0()
    {
        var scope = _container.CreateScope();
        _ = scope.GetService<IVoidSvc_D0>()!;
    }

    [Benchmark(Description = "Resolve IVoidSvc D1")]
    public void Void_D1()
    {
        var scope = _container.CreateScope();
        _ = scope.GetService<IVoidSvc_D1>()!;
    }

    [Benchmark(Description = "Resolve IVoidSvc D3")]
    public void Void_D3()
    {
        var scope = _container.CreateScope();
        _ = scope.GetService<IVoidSvc_D3>()!;
    }

    [Benchmark(Description = "Resolve IVoidSvc D5")]
    public void Void_D5()
    {
        var scope = _container.CreateScope();
        _ = scope.GetService<IVoidSvc_D5>()!;
    }
}
