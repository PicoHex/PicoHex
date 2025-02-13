namespace PicoHex.Logger.Console;

public class Logger<T>(ILoggerFactory factory) : ILogger<T>
{
    private readonly ILogger _logger = factory.CreateLogger(typeof(T).FullName);

    public void Log(LogLevel level, string message, Exception? exception = null)
    {
        _logger.Log(level, message, exception);
    }

    public async ValueTask LogAsync(
        LogLevel level,
        string message,
        Exception? exception = null,
        CancellationToken cancellationToken = default
    )
    {
        await _logger.LogAsync(level, message, exception, cancellationToken);
    }

    public IDisposable BeginScope<TState>(TState state)
    {
        return new LogScope(state);
    }

    private void LogInternal(LogLevel level, string message, Exception? exception)
    {
        var scopes = CurrentScopes; // Implement CurrentScopes property
        var entry = new LogEntry
        {
            Timestamp = DateTime.UtcNow,
            Level = level,
            Message = message,
            Exception = exception,
            Scopes = scopes.Select(s => s.State).ToList()
        };

        _sink.Emit(entry);
    }
}
