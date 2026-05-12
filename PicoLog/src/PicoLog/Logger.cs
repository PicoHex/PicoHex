namespace PicoLog;

/// <summary>
/// Typed adapter for <see cref="ILogger{TCategory}"/>.
/// </summary>
public sealed class Logger<TCategory> : ILogger<TCategory>
{
    private readonly ILoggerFactory _factory;
    private readonly Lazy<ILogger> _innerLogger;

    public Logger(ILoggerFactory factory)
    {
        _factory = factory ?? throw new ArgumentNullException(nameof(factory));
        _innerLogger = new Lazy<ILogger>(() => _factory.CreateLogger(typeof(TCategory).FullName!));
    }

    private ILogger InnerLogger => _innerLogger.Value;

    public IDisposable BeginScope<TState>(TState state)
        where TState : notnull => InnerLogger.BeginScope(state);

    public void Log(LogLevel logLevel, string message, Exception? exception = null) =>
        InnerLogger.Log(logLevel, message, exception);

    public void Log(
        LogLevel logLevel,
        string message,
        IReadOnlyList<KeyValuePair<string, object?>>? properties,
        Exception? exception
    ) => InnerLogger.Log(logLevel, message, properties, exception);

    public Task LogAsync(
        LogLevel logLevel,
        string message,
        Exception? exception = null,
        CancellationToken cancellationToken = default
    ) => InnerLogger.LogAsync(logLevel, message, exception, cancellationToken);

    public Task LogAsync(
        LogLevel logLevel,
        string message,
        IReadOnlyList<KeyValuePair<string, object?>>? properties,
        Exception? exception,
        CancellationToken cancellationToken = default
    ) => InnerLogger.LogAsync(logLevel, message, properties, exception, cancellationToken);

    public void Log(LogLevel logLevel, FormattableString message, Exception? exception = null) =>
        InnerLogger.Log(logLevel, message, exception);

    public void Log(
        LogLevel logLevel,
        FormattableString message,
        IReadOnlyList<KeyValuePair<string, object?>>? properties,
        Exception? exception
    ) => InnerLogger.Log(logLevel, message, properties, exception);

    public Task LogAsync(
        LogLevel logLevel,
        FormattableString message,
        Exception? exception = null,
        CancellationToken cancellationToken = default
    ) => InnerLogger.LogAsync(logLevel, message, exception, cancellationToken);

    public Task LogAsync(
        LogLevel logLevel,
        FormattableString message,
        IReadOnlyList<KeyValuePair<string, object?>>? properties,
        Exception? exception,
        CancellationToken cancellationToken = default
    ) => InnerLogger.LogAsync(logLevel, message, properties, exception, cancellationToken);

    public void Log(
        LogLevel logLevel,
        EventId eventId,
        string message,
        Exception? exception = null
    ) => InnerLogger.Log(logLevel, eventId, message, exception);

    public void Log(
        LogLevel logLevel,
        EventId eventId,
        string message,
        IReadOnlyList<KeyValuePair<string, object?>>? properties,
        Exception? exception
    ) => InnerLogger.Log(logLevel, eventId, message, properties, exception);

    public Task LogAsync(
        LogLevel logLevel,
        EventId eventId,
        string message,
        Exception? exception = null,
        CancellationToken cancellationToken = default
    ) => InnerLogger.LogAsync(logLevel, eventId, message, exception, cancellationToken);

    public Task LogAsync(
        LogLevel logLevel,
        EventId eventId,
        string message,
        IReadOnlyList<KeyValuePair<string, object?>>? properties,
        Exception? exception,
        CancellationToken cancellationToken = default
    ) => InnerLogger.LogAsync(logLevel, eventId, message, properties, exception, cancellationToken);

    public void Log(
        LogLevel logLevel,
        EventId eventId,
        FormattableString message,
        Exception? exception = null
    ) => InnerLogger.Log(logLevel, eventId, message, exception);

    public void Log(
        LogLevel logLevel,
        EventId eventId,
        FormattableString message,
        IReadOnlyList<KeyValuePair<string, object?>>? properties,
        Exception? exception
    ) => InnerLogger.Log(logLevel, eventId, message, properties, exception);

    public Task LogAsync(
        LogLevel logLevel,
        EventId eventId,
        FormattableString message,
        Exception? exception = null,
        CancellationToken cancellationToken = default
    ) => InnerLogger.LogAsync(logLevel, eventId, message, exception, cancellationToken);

    public Task LogAsync(
        LogLevel logLevel,
        EventId eventId,
        FormattableString message,
        IReadOnlyList<KeyValuePair<string, object?>>? properties,
        Exception? exception,
        CancellationToken cancellationToken = default
    ) => InnerLogger.LogAsync(logLevel, eventId, message, properties, exception, cancellationToken);
}
