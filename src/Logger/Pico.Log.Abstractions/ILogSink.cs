namespace Pico.Log.Abstractions;

public interface ILogSink : IDisposable, IAsyncDisposable
{
    ValueTask WriteAsync(LogEntry entry, CancellationToken cancellationToken = default);
}
