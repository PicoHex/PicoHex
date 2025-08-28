namespace Pico.Logger.Abs;

public static class LoggerExtensions
{
    public static void Trace(this ILogger logger, string message) =>
        logger.Log(LogLevel.Trace, message);

    public static void Debug(this ILogger logger, string message, Exception? exception = null) =>
        logger.Log(LogLevel.Debug, message, exception);

    public static void Info(this ILogger logger, string message) =>
        logger.Log(LogLevel.Info, message);

    public static void Notice(this ILogger logger, string message, Exception? exception = null) =>
        logger.Log(LogLevel.Notice, message, exception);

    public static void Warning(this ILogger logger, string message, Exception? exception = null) =>
        logger.Log(LogLevel.Warning, message, exception);

    public static void Error(this ILogger logger, string message, Exception? exception = null) =>
        logger.Log(LogLevel.Error, message, exception);

    public static void Critical(this ILogger logger, string message, Exception? exception = null) =>
        logger.Log(LogLevel.Critical, message, exception);

    public static void Alert(this ILogger logger, string message, Exception? exception = null) =>
        logger.Log(LogLevel.Alert, message, exception);

    public static void Emergency(
        this ILogger logger,
        string message,
        Exception? exception = null
    ) => logger.Log(LogLevel.Emergency, message, exception);

    public static ValueTask TraceAsync(
        this ILogger logger,
        string message,
        CancellationToken cancellationToken = default
    ) => logger.LogAsync(LogLevel.Trace, message, cancellationToken: cancellationToken);

    public static ValueTask DebugAsync(
        this ILogger logger,
        string message,
        Exception? exception = null,
        CancellationToken cancellationToken = default
    ) => logger.LogAsync(LogLevel.Debug, message, exception, cancellationToken: cancellationToken);

    public static ValueTask InfoAsync(
        this ILogger logger,
        string message,
        CancellationToken cancellationToken = default
    ) => logger.LogAsync(LogLevel.Info, message, cancellationToken: cancellationToken);

    public static ValueTask NoticeAsync(
        this ILogger logger,
        string message,
        Exception? exception = null,
        CancellationToken cancellationToken = default
    ) => logger.LogAsync(LogLevel.Notice, message, exception, cancellationToken);

    public static ValueTask WarningAsync(
        this ILogger logger,
        string message,
        Exception? exception = null,
        CancellationToken cancellationToken = default
    ) => logger.LogAsync(LogLevel.Warning, message, exception, cancellationToken);

    public static ValueTask ErrorAsync(
        this ILogger logger,
        string message,
        Exception? exception = null,
        CancellationToken cancellationToken = default
    ) => logger.LogAsync(LogLevel.Error, message, exception, cancellationToken);

    public static ValueTask CriticalAsync(
        this ILogger logger,
        string message,
        Exception? exception = null,
        CancellationToken cancellationToken = default
    ) => logger.LogAsync(LogLevel.Critical, message, exception, cancellationToken);

    public static ValueTask AlertAsync(
        this ILogger logger,
        string message,
        Exception? exception = null,
        CancellationToken cancellationToken = default
    ) => logger.LogAsync(LogLevel.Alert, message, exception, cancellationToken);

    public static ValueTask EmergencyAsync(
        this ILogger logger,
        string message,
        Exception? exception = null,
        CancellationToken cancellationToken = default
    ) => logger.LogAsync(LogLevel.Emergency, message, exception, cancellationToken);
}
