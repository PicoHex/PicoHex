namespace PicoHex.Log.NG;

public class Logger<T>(ILoggerFactory factory) : ILogger<T>
{
    private readonly ILogger _logger = factory.CreateLogger(typeof(T).FullName!);

    public IDisposable BeginScope<TState>(TState state) => _logger.BeginScope(state);

    public void Log(LogLevel logLevel, string message, Exception? exception = null) =>
        _logger.Log(logLevel, message, exception);
}
