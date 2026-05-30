namespace PicoLog.Abs;

public interface ILogSink : IAsyncDisposable
{
    /// <summary>
    /// Writes a log entry to the sink.
    /// </summary>
    /// <remarks>
    /// A single sink instance can be shared across multiple category loggers within the same
    /// <see cref="ILoggerFactory"/>. Implementations must therefore tolerate concurrent
    /// <see cref="WriteAsync(LogEntry, CancellationToken)"/> calls and synchronize any shared
    /// mutable state or writers as needed.
    /// </remarks>
    Task WriteAsync(LogEntry entry, CancellationToken cancellationToken = default);
}

/// <summary>
/// Optional interface for sinks that can efficiently handle batch writes.
/// When a sink implements this interface, the logging pipeline dispatches
/// entries in batches rather than one-at-a-time, reducing per-entry overhead.
/// </summary>
public interface IBatchingLogSink : ILogSink
{
    /// <summary>
    /// Writes a batch of log entries to the sink in a single operation.
    /// </summary>
    ValueTask WriteBatchAsync(
        IReadOnlyList<LogEntry> batch,
        CancellationToken cancellationToken = default
    );
}
