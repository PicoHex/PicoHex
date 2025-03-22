namespace PicoHex.Log.Abstractions;

public interface ILogSink : IDisposable
{
    ValueTask WriteAsync(LogEntry entry, CancellationToken cancellationToken = default);
}
