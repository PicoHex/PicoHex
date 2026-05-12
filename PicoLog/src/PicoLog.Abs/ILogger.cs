namespace PicoLog.Abs;

/// <summary>
/// Provides logging methods with overloads for every combination of:
/// <list type="bullet">
///   <item><description><b>Message format</b> — plain <see cref="string"/>, <see cref="FormattableString"/>, or <see cref="EventId"/>-qualified.</description></item>
///   <item><description><b>Sync/Async</b> — synchronous <see cref="Log"/> for fire-and-forget, <see cref="LogAsync"/> for backpressure-aware producers.</description></item>
///   <item><description><b>Structured properties</b> — each overload pair has a variant accepting <c>IReadOnlyList&lt;KeyValuePair&lt;string, object?&gt;&gt;?</c>.</description></item>
/// </list>
/// This yields 24 overloads (3 message formats × 2 sync/async × 2 property presence × 2 with/without exception default = 24).
/// The design avoids boxing and conditional branches at the call site so that source generators and AOT compilers can
/// emit direct dispatch with minimal metadata overhead.
/// </summary>
public interface ILogger
{
    IDisposable BeginScope<TState>(TState state)
        where TState : notnull;

    void Log(LogLevel logLevel, string message, Exception? exception = null);

    void Log(
        LogLevel logLevel,
        string message,
        IReadOnlyList<KeyValuePair<string, object?>>? properties,
        Exception? exception
    );

    /// <summary>
    /// Asynchronously logs a message.
    /// </summary>
    /// <remarks>
    /// Completion indicates that the logger accepted the write or finished any configured
    /// backpressure handling at the logger boundary. It does not, by itself, guarantee that the
    /// entry has already been durably written by downstream sinks.
    /// </remarks>
    Task LogAsync(
        LogLevel logLevel,
        string message,
        Exception? exception = null,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Asynchronously logs a message with optional structured properties.
    /// </summary>
    /// <remarks>
    /// Completion indicates that the logger accepted the write or finished any configured
    /// backpressure handling at the logger boundary. It does not, by itself, guarantee that the
    /// entry has already been durably written by downstream sinks.
    /// </remarks>
    Task LogAsync(
        LogLevel logLevel,
        string message,
        IReadOnlyList<KeyValuePair<string, object?>>? properties,
        Exception? exception,
        CancellationToken cancellationToken = default
    );

    void Log(LogLevel logLevel, FormattableString message, Exception? exception = null);

    void Log(
        LogLevel logLevel,
        FormattableString message,
        IReadOnlyList<KeyValuePair<string, object?>>? properties,
        Exception? exception
    );

    /// <summary>
    /// Asynchronously logs a message.
    /// </summary>
    /// <remarks>
    /// Completion indicates that the logger accepted the write or finished any configured
    /// backpressure handling at the logger boundary. It does not, by itself, guarantee that the
    /// entry has already been durably written by downstream sinks.
    /// </remarks>
    Task LogAsync(
        LogLevel logLevel,
        FormattableString message,
        Exception? exception = null,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Asynchronously logs a message with optional structured properties.
    /// </summary>
    /// <remarks>
    /// Completion indicates that the logger accepted the write or finished any configured
    /// backpressure handling at the logger boundary. It does not, by itself, guarantee that the
    /// entry has already been durably written by downstream sinks.
    /// </remarks>
    Task LogAsync(
        LogLevel logLevel,
        FormattableString message,
        IReadOnlyList<KeyValuePair<string, object?>>? properties,
        Exception? exception,
        CancellationToken cancellationToken = default
    );

    void Log(LogLevel logLevel, EventId eventId, string message, Exception? exception = null);

    void Log(
        LogLevel logLevel,
        EventId eventId,
        string message,
        IReadOnlyList<KeyValuePair<string, object?>>? properties,
        Exception? exception
    );

    /// <summary>
    /// Asynchronously logs a message.
    /// </summary>
    /// <remarks>
    /// Completion indicates that the logger accepted the write or finished any configured
    /// backpressure handling at the logger boundary. It does not, by itself, guarantee that the
    /// entry has already been durably written by downstream sinks.
    /// </remarks>
    Task LogAsync(
        LogLevel logLevel,
        EventId eventId,
        string message,
        Exception? exception = null,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Asynchronously logs a message with optional structured properties.
    /// </summary>
    /// <remarks>
    /// Completion indicates that the logger accepted the write or finished any configured
    /// backpressure handling at the logger boundary. It does not, by itself, guarantee that the
    /// entry has already been durably written by downstream sinks.
    /// </remarks>
    Task LogAsync(
        LogLevel logLevel,
        EventId eventId,
        string message,
        IReadOnlyList<KeyValuePair<string, object?>>? properties,
        Exception? exception,
        CancellationToken cancellationToken = default
    );

    void Log(
        LogLevel logLevel,
        EventId eventId,
        FormattableString message,
        Exception? exception = null
    );

    void Log(
        LogLevel logLevel,
        EventId eventId,
        FormattableString message,
        IReadOnlyList<KeyValuePair<string, object?>>? properties,
        Exception? exception
    );

    /// <summary>
    /// Asynchronously logs a message.
    /// </summary>
    /// <remarks>
    /// Completion indicates that the logger accepted the write or finished any configured
    /// backpressure handling at the logger boundary. It does not, by itself, guarantee that the
    /// entry has already been durably written by downstream sinks.
    /// </remarks>
    Task LogAsync(
        LogLevel logLevel,
        EventId eventId,
        FormattableString message,
        Exception? exception = null,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Asynchronously logs a message with optional structured properties.
    /// </summary>
    /// <remarks>
    /// Completion indicates that the logger accepted the write or finished any configured
    /// backpressure handling at the logger boundary. It does not, by itself, guarantee that the
    /// entry has already been durably written by downstream sinks.
    /// </remarks>
    Task LogAsync(
        LogLevel logLevel,
        EventId eventId,
        FormattableString message,
        IReadOnlyList<KeyValuePair<string, object?>>? properties,
        Exception? exception,
        CancellationToken cancellationToken = default
    );
}

public interface ILogger<out TCategory> : ILogger;
