namespace PicoHex.Core.Logging;

public interface ILogSink : IDisposable, IAsyncDisposable
{
    LogLevel MinimumLevel { get; set; }

    void Emit(LogEntry entry);

    ValueTask EmitAsync(LogEntry entry, CancellationToken cancellationToken = default);
}
