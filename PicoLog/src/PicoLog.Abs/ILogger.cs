namespace PicoLog.Abs;

/// <summary>
/// Core logging interface.
/// <para>
/// The interface exposes 8 methods — every combination of
/// <b>message format</b> (plain string / <see cref="FormattableString"/> / <see cref="EventId"/>-qualified)
/// × <b>sync/async</b> with all parameters explicit.
/// </para>
/// <para>
/// Convenience overloads with default parameters are provided as extension methods.
/// This avoids the previous 24-overload interface bloat while keeping the
/// FormattableString and EventId paths on the interface (they need internal
/// LogEntry construction details that extension methods cannot access).
/// </para>
/// </summary>
public interface ILogger
{
    IDisposable BeginScope<TState>(TState state)
        where TState : notnull;

    // ── string ──

    void Log(
        LogLevel logLevel,
        string message,
        IReadOnlyList<KeyValuePair<string, object?>>? properties,
        Exception? exception
    );

    Task LogAsync(
        LogLevel logLevel,
        string message,
        IReadOnlyList<KeyValuePair<string, object?>>? properties,
        Exception? exception,
        CancellationToken cancellationToken
    );

    // ── EventId + string ──

    void Log(
        LogLevel logLevel,
        EventId eventId,
        string message,
        IReadOnlyList<KeyValuePair<string, object?>>? properties,
        Exception? exception
    );

    Task LogAsync(
        LogLevel logLevel,
        EventId eventId,
        string message,
        IReadOnlyList<KeyValuePair<string, object?>>? properties,
        Exception? exception,
        CancellationToken cancellationToken
    );

    // ── FormattableString ──

    void Log(
        LogLevel logLevel,
        FormattableString message,
        IReadOnlyList<KeyValuePair<string, object?>>? properties,
        Exception? exception
    );

    Task LogAsync(
        LogLevel logLevel,
        FormattableString message,
        IReadOnlyList<KeyValuePair<string, object?>>? properties,
        Exception? exception,
        CancellationToken cancellationToken
    );

    // ── EventId + FormattableString ──

    void Log(
        LogLevel logLevel,
        EventId eventId,
        FormattableString message,
        IReadOnlyList<KeyValuePair<string, object?>>? properties,
        Exception? exception
    );

    Task LogAsync(
        LogLevel logLevel,
        EventId eventId,
        FormattableString message,
        IReadOnlyList<KeyValuePair<string, object?>>? properties,
        Exception? exception,
        CancellationToken cancellationToken
    );
}

public interface ILogger<out TCategory> : ILogger;
