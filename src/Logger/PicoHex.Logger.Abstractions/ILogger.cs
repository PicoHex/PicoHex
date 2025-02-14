namespace PicoHex.Logger.Abstractions;

/// <summary>
/// Represents the core logging interface for writing log entries.
/// </summary>
/// <remarks>
/// Provides both synchronous and asynchronous methods for logging messages at specific severity levels.
/// Implementations should ensure thread safety for concurrent operations.
/// </remarks>
public interface ILogger
{
    /// <summary>
    /// Logs a message with the specified severity level and optional exception.
    /// </summary>
    /// <param name="level">The severity level of the log entry</param>
    /// <param name="message">The log message content</param>
    /// <param name="exception">Optional exception associated with the log entry</param>
    /// <remarks>
    /// <para>
    /// This synchronous method should be used for non-I/O intensive logging operations.
    /// For I/O-bound operations, prefer the asynchronous <see cref="LogAsync"/> method.
    /// </para>
    /// <para>
    /// Implementations should:
    /// - Handle null exceptions gracefully
    /// - Prevent message null references
    /// - Format exceptions appropriately when present
    /// </para>
    /// </remarks>
    void Log(LogLevel level, string message, Exception? exception = null);

    /// <summary>
    /// Asynchronously logs a message with the specified severity level and optional exception.
    /// </summary>
    /// <param name="level">The severity level of the log entry</param>
    /// <param name="message">The log message content</param>
    /// <param name="exception">Optional exception associated with the log entry</param>
    /// <param name="cancellationToken">Token to monitor for cancellation requests</param>
    /// <returns>A ValueTask representing the asynchronous operation</returns>
    /// <remarks>
    /// <para>
    /// Preferred method for I/O-bound logging operations. Implementations should:
    /// - Respect the cancellation token
    /// - Use asynchronous I/O operations
    /// - Handle exceptions internally where appropriate
    /// </para>
    /// <para>
    /// Callers should typically await this method to ensure proper error handling.
    /// </para>
    /// </remarks>
    ValueTask LogAsync(
        LogLevel level,
        string message,
        Exception? exception = null,
        CancellationToken cancellationToken = default
    );

    IDisposable BeginScope<TState>(TState state);
}

/// <summary>
/// Generic variant of <see cref="ILogger"/> that associates logs with a specific category type.
/// </summary>
/// <typeparam name="TCategory">The type used to define the logging category</typeparam>
/// <remarks>
/// <para>
/// Typically used through dependency injection where the consumer type becomes the log category.
/// The category name is usually derived from the full type name of <typeparamref name="TCategory"/>.
/// </para>
/// <para>
/// Example usage in a service class:
/// <code>ILogger&lt;OrderService&gt; logger;</code>
/// </para>
/// </remarks>
public interface ILogger<out TCategory> : ILogger;

/// <summary>
/// Provides extension methods for <see cref="ILogger"/> to enable level-specific logging.
/// </summary>
/// <remarks>
/// Contains convenience methods for all defined <see cref="LogLevel"/> values,
/// offering both synchronous and asynchronous logging variants.
/// </remarks>
public static class LoggerExtensions
{
    /// <summary>
    /// Logs a message at <see cref="LogLevel.Trace"/> level.
    /// </summary>
    /// <remarks>
    /// Use for detailed debugging information that's typically only relevant during development.
    /// </remarks>
    public static void Trace(this ILogger logger, string message, Exception? exception = null) =>
        logger.Log(LogLevel.Trace, message, exception);

    /// <summary>
    /// Logs a message at <see cref="LogLevel.Debug"/> level.
    /// </summary>
    /// <remarks>
    /// Use for debugging information that's useful in production troubleshooting.
    /// </remarks>
    public static void Debug(this ILogger logger, string message, Exception? exception = null) =>
        logger.Log(LogLevel.Debug, message, exception);

    /// <summary>
    /// Logs a message at <see cref="LogLevel.Information"/> level.
    /// </summary>
    /// <remarks>
    /// Use for general application flow tracking and significant events.
    /// </remarks>
    public static void Info(this ILogger logger, string message, Exception? exception = null) =>
        logger.Log(LogLevel.Information, message, exception);

    /// <summary>
    /// Logs a message at <see cref="LogLevel.Notice"/> level.
    /// </summary>
    /// <remarks>
    /// Use for important runtime events that require attention but aren't errors.
    /// </remarks>
    public static void Notice(this ILogger logger, string message, Exception? exception = null) =>
        logger.Log(LogLevel.Notice, message, exception);

    /// <summary>
    /// Logs a message at <see cref="LogLevel.Warning"/> level.
    /// </summary>
    /// <remarks>
    /// Use for unexpected or problematic situations that aren't immediately harmful.
    /// </remarks>
    public static void Warning(this ILogger logger, string message, Exception? exception = null) =>
        logger.Log(LogLevel.Warning, message, exception);

    /// <summary>
    /// Logs a message at <see cref="LogLevel.Error"/> level.
    /// </summary>
    /// <remarks>
    /// Use for errors that impact specific operations but allow the application to continue.
    /// </remarks>
    public static void Error(this ILogger logger, string message, Exception? exception = null) =>
        logger.Log(LogLevel.Error, message, exception);

    /// <summary>
    /// Logs a message at <see cref="LogLevel.Critical"/> level.
    /// </summary>
    /// <remarks>
    /// Use for severe failures that require immediate attention and may crash the application.
    /// </remarks>
    public static void Critical(this ILogger logger, string message, Exception? exception = null) =>
        logger.Log(LogLevel.Critical, message, exception);

    /// <summary>
    /// Logs a message at <see cref="LogLevel.Alert"/> level.
    /// </summary>
    /// <remarks>
    /// Use when immediate action is required (e.g., loss of primary database connection).
    /// </remarks>
    public static void Alert(this ILogger logger, string message, Exception? exception = null) =>
        logger.Log(LogLevel.Alert, message, exception);

    /// <summary>
    /// Logs a message at <see cref="LogLevel.Emergency"/> level.
    /// </summary>
    /// <remarks>
    /// Reserve for catastrophic system-wide failures where the system is unusable.
    /// </remarks>
    public static void Emergency(
        this ILogger logger,
        string message,
        Exception? exception = null
    ) => logger.Log(LogLevel.Emergency, message, exception);

    /// <summary>
    /// Asynchronously logs a message at <see cref="LogLevel.Trace"/> level.
    /// </summary>
    /// <remarks>
    /// Prefer this async version for I/O-bound operations in async contexts.
    /// </remarks>
    public static ValueTask TraceAsync(
        this ILogger logger,
        string message,
        Exception? exception = null,
        CancellationToken cancellationToken = default
    ) => logger.LogAsync(LogLevel.Trace, message, exception, cancellationToken);

    /// <summary>
    /// Asynchronously logs a message at <see cref="LogLevel.Debug"/> level.
    /// </summary>
    /// <remarks>
    /// Use for debugging information that's useful in production troubleshooting.
    /// </remarks>
    public static ValueTask DebugAsync(
        this ILogger logger,
        string message,
        Exception? exception = null,
        CancellationToken cancellationToken = default
    ) => logger.LogAsync(LogLevel.Debug, message, exception, cancellationToken);

    /// <summary>
    /// Asynchronously logs a message at <see cref="LogLevel.Information"/> level.
    /// </summary>
    /// <remarks>
    /// Use for general application flow tracking and significant events.
    /// </remarks>
    public static ValueTask InfoAsync(
        this ILogger logger,
        string message,
        Exception? exception = null,
        CancellationToken cancellationToken = default
    ) => logger.LogAsync(LogLevel.Information, message, exception, cancellationToken);

    /// <summary>
    /// Asynchronously logs a message at <see cref="LogLevel.Notice"/> level.
    /// </summary>
    /// <remarks>
    /// Use for important runtime events that require attention but aren't errors.
    /// </remarks>
    public static ValueTask NoticeAsync(
        this ILogger logger,
        string message,
        Exception? exception = null,
        CancellationToken cancellationToken = default
    ) => logger.LogAsync(LogLevel.Notice, message, exception, cancellationToken);

    /// <summary>
    /// Asynchronously logs a message at <see cref="LogLevel.Warning"/> level.
    /// </summary>
    /// <remarks>
    /// Use for unexpected or problematic situations that aren't immediately harmful.
    /// </remarks>
    public static ValueTask WarningAsync(
        this ILogger logger,
        string message,
        Exception? exception = null,
        CancellationToken cancellationToken = default
    ) => logger.LogAsync(LogLevel.Warning, message, exception, cancellationToken);

    /// <summary>
    /// Asynchronously logs a message at <see cref="LogLevel.Error"/> level.
    /// </summary>
    /// <remarks>
    /// Use for errors that impact specific operations but allow the application to continue.
    /// </remarks>
    public static ValueTask ErrorAsync(
        this ILogger logger,
        string message,
        Exception? exception = null,
        CancellationToken cancellationToken = default
    ) => logger.LogAsync(LogLevel.Error, message, exception, cancellationToken);

    /// <summary>
    /// Asynchronously logs a message at <see cref="LogLevel.Critical"/> level.
    /// </summary>
    /// <remarks>
    /// Use for severe failures that require immediate attention and may crash the application.
    /// </remarks>
    public static ValueTask CriticalAsync(
        this ILogger logger,
        string message,
        Exception? exception = null,
        CancellationToken cancellationToken = default
    ) => logger.LogAsync(LogLevel.Critical, message, exception, cancellationToken);

    /// <summary>
    /// Asynchronously logs a message at <see cref="LogLevel.Alert"/> level.
    /// </summary>
    /// <remarks>
    /// Use when immediate action is required (e.g., loss of primary database connection).
    /// </remarks>
    public static ValueTask AlertAsync(
        this ILogger logger,
        string message,
        Exception? exception = null,
        CancellationToken cancellationToken = default
    ) => logger.LogAsync(LogLevel.Alert, message, exception, cancellationToken);

    /// <summary>
    /// Asynchronously logs a message at <see cref="LogLevel.Emergency"/> level.
    /// </summary>
    /// <remarks>
    /// Reserve for catastrophic system-wide failures where the system is unusable.
    /// </remarks>
    public static ValueTask EmergencyAsync(
        this ILogger logger,
        string message,
        Exception? exception = null,
        CancellationToken cancellationToken = default
    ) => logger.LogAsync(LogLevel.Emergency, message, exception, cancellationToken);
}
