namespace PicoHex.Log;

public class Logger<T>(ILoggerFactory factory) : ILogger<T>
{
    private readonly ILogger _logger = factory.CreateLogger(typeof(T).FullName!);

    public IDisposable BeginScope<TState>(TState state) => _logger.BeginScope(state);

    public void Log(LogLevel logLevel, string message, Exception? exception = null) =>
        _logger.Log(logLevel, message, exception);

    public async ValueTask LogAsync(
        LogLevel logLevel,
        string message,
        Exception? exception = null,
        CancellationToken cancellationToken = default
    )
    {
        await _logger.LogAsync(logLevel, message, exception, cancellationToken);
    }
}
