namespace PicoHex.Core.Logging;

/// <summary>
/// Defines a destination for log entries in the logging framework.
/// </summary>
/// <remarks>
/// Implement this interface to create custom log targets (e.g., console, file, database, or external services).
/// The sink will only process log entries that meet or exceed the <see cref="MinimumLevel"/> threshold.
/// </remarks>
public interface ILogSink : IDisposable, IAsyncDisposable
{
    /// <summary>
    /// Gets or sets the minimum severity level required for log entries to be processed by this sink.
    /// </summary>
    /// <value>
    /// A <see cref="LogLevel"/> value indicating the minimum severity that will trigger logging.
    /// Entries with lower severity levels will be ignored by this sink.
    /// </value>
    /// <example>
    /// Setting this to <see cref="LogLevel.Warning"/> will log only warnings, errors, and critical events.
    /// </example>
    LogLevel MinimumLevel { get; set; }

    /// <summary>
    /// Synchronously writes a log entry to the sink.
    /// </summary>
    /// <param name="entry">The log entry to write</param>
    /// <remarks>
    /// Implementations should:
    /// - Filter entries below <see cref="MinimumLevel"/> before processing
    /// - Handle any sink-specific formatting or serialization
    /// - Ensure thread safety for concurrent writes
    /// - Handle errors internally where appropriate
    /// </remarks>
    void Emit(LogEntry entry);

    /// <summary>
    /// Asynchronously writes a log entry to the sink.
    /// </summary>
    /// <param name="entry">The log entry to write</param>
    /// <param name="cancellationToken">A token that may be used to cancel the write operation</param>
    /// <returns>A ValueTask representing the asynchronous operation</returns>
    /// <remarks>
    /// Implementations should:
    /// - Prefer asynchronous I/O operations where possible
    /// - Respect the cancellation token for graceful termination
    /// - Handle errors appropriately (e.g., return failed tasks rather than throw exceptions)
    /// - Ensure proper resource cleanup in case of cancellation
    /// </remarks>
    ValueTask EmitAsync(LogEntry entry, CancellationToken cancellationToken = default);
}
