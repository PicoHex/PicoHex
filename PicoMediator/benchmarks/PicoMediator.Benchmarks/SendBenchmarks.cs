namespace PicoMediator.Benchmarks;

[BenchmarkClass(Description = "Mediator Send — throughput vs raw handler call")]
public partial class SendBenchmarks
{
    private SvcContainer _container = null!;
    private ISvcScope _scope = null!;
    private IMediator _mediator = null!;
    private IRequestHandler<Ping, string> _handler = null!;
    private Ping _request = null!;

    [GlobalSetup]
    public void Setup()
    {
        _container = new SvcContainer();
        _container.RegisterTransient<IRequestHandler<Ping, string>>(_ => new PingHandler());
        _container.AddPicoMediator();
        _container.Build();

        _scope = _container.CreateScope();
        _mediator = _scope.GetService<IMediator>();
        _handler = _scope.GetService<IRequestHandler<Ping, string>>();
        _request = new Ping();
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _scope.DisposeAsync().AsTask().GetAwaiter().GetResult();
        _container.DisposeAsync().AsTask().GetAwaiter().GetResult();
    }

    [Benchmark(Baseline = true, Description = "Raw handler call")]
    public ValueTask<string> RawHandler() => _handler.Handle(_request, default);

    [Benchmark(Description = "Mediator.Send")]
    public ValueTask<string> MediatorSend() => _mediator.Send<Ping, string>(_request);

    public record Ping : IRequest<string>;

    public sealed class PingHandler : IRequestHandler<Ping, string>
    {
        public ValueTask<string> Handle(Ping r, CancellationToken ct) =>
            ValueTask.FromResult("pong");
    }
}
