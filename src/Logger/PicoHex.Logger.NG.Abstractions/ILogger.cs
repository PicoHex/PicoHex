namespace PicoHex.Logger.NG.Abstractions;

public interface ILogger
{
    void Log(LogLevel level, string message, Exception? exception = null);
    ValueTask LogAsync(
        LogLevel level,
        string message,
        Exception? exception = null,
        CancellationToken cancellationToken = default
    );
}

public interface ILogger<out T> : ILogger;

public static class LoggerExtensions
{
    public static void LogTrace(this ILogger logger, string message, Exception? exception = null) =>
        logger.Log(LogLevel.Trace, message, exception);

    public static void LogDebug(this ILogger logger, string message, Exception? exception = null) =>
        logger.Log(LogLevel.Debug, message, exception);

    public static void LogInformation(
        this ILogger logger,
        string message,
        Exception? exception = null
    ) => logger.Log(LogLevel.Information, message, exception);

    public static void LogNotice(
        this ILogger logger,
        string message,
        Exception? exception = null
    ) => logger.Log(LogLevel.Notice, message, exception);

    public static void LogWarning(
        this ILogger logger,
        string message,
        Exception? exception = null
    ) => logger.Log(LogLevel.Warning, message, exception);

    public static void LogError(this ILogger logger, string message, Exception? exception = null) =>
        logger.Log(LogLevel.Error, message, exception);

    public static void LogCritical(
        this ILogger logger,
        string message,
        Exception? exception = null
    ) => logger.Log(LogLevel.Critical, message, exception);

    public static void LogAlert(this ILogger logger, string message, Exception? exception = null) =>
        logger.Log(LogLevel.Alert, message, exception);

    public static void LogEmergency(
        this ILogger logger,
        string message,
        Exception? exception = null
    ) => logger.Log(LogLevel.Emergency, message, exception);

    public static ValueTask LogTraceAsync(
        this ILogger logger,
        string message,
        Exception? exception = null,
        CancellationToken cancellationToken = default
    ) => logger.LogAsync(LogLevel.Trace, message, exception, cancellationToken);

    public static ValueTask LogDebugAsync(
        this ILogger logger,
        string message,
        Exception? exception = null,
        CancellationToken cancellationToken = default
    ) => logger.LogAsync(LogLevel.Debug, message, exception, cancellationToken);

    public static ValueTask LogInformationAsync(
        this ILogger logger,
        string message,
        Exception? exception = null,
        CancellationToken cancellationToken = default
    ) => logger.LogAsync(LogLevel.Information, message, exception, cancellationToken);

    public static ValueTask LogNoticeAsync(
        this ILogger logger,
        string message,
        Exception? exception = null,
        CancellationToken cancellationToken = default
    ) => logger.LogAsync(LogLevel.Notice, message, exception, cancellationToken);

    public static ValueTask LogWarningAsync(
        this ILogger logger,
        string message,
        Exception? exception = null,
        CancellationToken cancellationToken = default
    ) => logger.LogAsync(LogLevel.Warning, message, exception, cancellationToken);

    public static ValueTask LogErrorAsync(
        this ILogger logger,
        string message,
        Exception? exception = null,
        CancellationToken cancellationToken = default
    ) => logger.LogAsync(LogLevel.Error, message, exception, cancellationToken);

    public static ValueTask LogCriticalAsync(
        this ILogger logger,
        string message,
        Exception? exception = null,
        CancellationToken cancellationToken = default
    ) => logger.LogAsync(LogLevel.Critical, message, exception, cancellationToken);

    public static ValueTask LogAlertAsync(
        this ILogger logger,
        string message,
        Exception? exception = null,
        CancellationToken cancellationToken = default
    ) => logger.LogAsync(LogLevel.Alert, message, exception, cancellationToken);

    public static ValueTask LogEmergencyAsync(
        this ILogger logger,
        string message,
        Exception? exception = null,
        CancellationToken cancellationToken = default
    ) => logger.LogAsync(LogLevel.Emergency, message, exception, cancellationToken);
}
