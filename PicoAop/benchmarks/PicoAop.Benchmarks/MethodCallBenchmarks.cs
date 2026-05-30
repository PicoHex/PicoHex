namespace PicoAop.Benchmarks;

[BenchmarkClass(Description = "void Do() — decorator chain overhead")]
public partial class MethodCallVoidBenchmarks
{
    private SvcContainer _container = null!;
    private ISvcScope _scope = null!;
    private IVoidSvc_D0 _svc0 = null!;
    private IVoidSvc_D1 _svc1 = null!;
    private IVoidSvc_D3 _svc3 = null!;
    private IVoidSvc_D5 _svc5 = null!;

    [GlobalSetup]
    public void Setup()
    {
        _container = ContainerFactory.Create();
        _scope = _container.CreateScope();
        _svc0 = _scope.GetService<IVoidSvc_D0>()!;
        _svc1 = _scope.GetService<IVoidSvc_D1>()!;
        _svc3 = _scope.GetService<IVoidSvc_D3>()!;
        _svc5 = _scope.GetService<IVoidSvc_D5>()!;
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _scope.DisposeAsync().AsTask().GetAwaiter().GetResult();
        _container.DisposeAsync().AsTask().GetAwaiter().GetResult();
    }

    [Benchmark(Baseline = true, Description = "D0 (raw)")]
    public void D0() => _svc0.Do();

    [Benchmark(Description = "D1")]
    public void D1() => _svc1.Do();

    [Benchmark(Description = "D3")]
    public void D3() => _svc3.Do();

    [Benchmark(Description = "D5")]
    public void D5() => _svc5.Do();
}

[BenchmarkClass(Description = "T Get() — decorator chain overhead")]
public partial class MethodCallReturnBenchmarks
{
    private SvcContainer _container = null!;
    private ISvcScope _scope = null!;
    private IReturnSvc_D0 _svc0 = null!;
    private IReturnSvc_D1 _svc1 = null!;
    private IReturnSvc_D3 _svc3 = null!;
    private IReturnSvc_D5 _svc5 = null!;

    [GlobalSetup]
    public void Setup()
    {
        _container = ContainerFactory.Create();
        _scope = _container.CreateScope();
        _svc0 = _scope.GetService<IReturnSvc_D0>()!;
        _svc1 = _scope.GetService<IReturnSvc_D1>()!;
        _svc3 = _scope.GetService<IReturnSvc_D3>()!;
        _svc5 = _scope.GetService<IReturnSvc_D5>()!;
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _scope.DisposeAsync().AsTask().GetAwaiter().GetResult();
        _container.DisposeAsync().AsTask().GetAwaiter().GetResult();
    }

    [Benchmark(Baseline = true, Description = "D0 (raw)")]
    public int D0() => _svc0.Get();

    [Benchmark(Description = "D1")]
    public int D1() => _svc1.Get();

    [Benchmark(Description = "D3")]
    public int D3() => _svc3.Get();

    [Benchmark(Description = "D5")]
    public int D5() => _svc5.Get();
}

[BenchmarkClass(Description = "ValueTask DoAsync() — decorator chain overhead")]
public partial class MethodCallTaskVoidBenchmarks
{
    private SvcContainer _container = null!;
    private ISvcScope _scope = null!;
    private ITaskVoidSvc_D0 _svc0 = null!;
    private ITaskVoidSvc_D1 _svc1 = null!;
    private ITaskVoidSvc_D3 _svc3 = null!;
    private ITaskVoidSvc_D5 _svc5 = null!;

    [GlobalSetup]
    public void Setup()
    {
        _container = ContainerFactory.Create();
        _scope = _container.CreateScope();
        _svc0 = _scope.GetService<ITaskVoidSvc_D0>()!;
        _svc1 = _scope.GetService<ITaskVoidSvc_D1>()!;
        _svc3 = _scope.GetService<ITaskVoidSvc_D3>()!;
        _svc5 = _scope.GetService<ITaskVoidSvc_D5>()!;
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _scope.DisposeAsync().AsTask().GetAwaiter().GetResult();
        _container.DisposeAsync().AsTask().GetAwaiter().GetResult();
    }

    [Benchmark(Baseline = true, Description = "D0 (raw)")]
    public ValueTask D0() => _svc0.DoAsync();

    [Benchmark(Description = "D1")]
    public ValueTask D1() => _svc1.DoAsync();

    [Benchmark(Description = "D3")]
    public ValueTask D3() => _svc3.DoAsync();

    [Benchmark(Description = "D5")]
    public ValueTask D5() => _svc5.DoAsync();
}

[BenchmarkClass(Description = "ValueTask<T> GetAsync() — decorator chain overhead")]
public partial class MethodCallTaskReturnBenchmarks
{
    private SvcContainer _container = null!;
    private ISvcScope _scope = null!;
    private ITaskReturnSvc_D0 _svc0 = null!;
    private ITaskReturnSvc_D1 _svc1 = null!;
    private ITaskReturnSvc_D3 _svc3 = null!;
    private ITaskReturnSvc_D5 _svc5 = null!;

    [GlobalSetup]
    public void Setup()
    {
        _container = ContainerFactory.Create();
        _scope = _container.CreateScope();
        _svc0 = _scope.GetService<ITaskReturnSvc_D0>()!;
        _svc1 = _scope.GetService<ITaskReturnSvc_D1>()!;
        _svc3 = _scope.GetService<ITaskReturnSvc_D3>()!;
        _svc5 = _scope.GetService<ITaskReturnSvc_D5>()!;
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _scope.DisposeAsync().AsTask().GetAwaiter().GetResult();
        _container.DisposeAsync().AsTask().GetAwaiter().GetResult();
    }

    [Benchmark(Baseline = true, Description = "D0 (raw)")]
    public ValueTask<int> D0() => _svc0.GetAsync();

    [Benchmark(Description = "D1")]
    public ValueTask<int> D1() => _svc1.GetAsync();

    [Benchmark(Description = "D3")]
    public ValueTask<int> D3() => _svc3.GetAsync();

    [Benchmark(Description = "D5")]
    public ValueTask<int> D5() => _svc5.GetAsync();
}
