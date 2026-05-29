namespace PicoLog.Benchmarks;

internal sealed class BackpressureSink(int spinIterations) : ILogSink
{
    public Task WriteAsync(LogEntry entry, CancellationToken cancellationToken = default)
    {
        Thread.SpinWait(spinIterations);
        return Task.CompletedTask;
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
