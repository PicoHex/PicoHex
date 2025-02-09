namespace PicoHex.Logger.NG;

public class Logger<T>(ILoggerFactory factory) : ILogger<T>
{
    private readonly ILogger _logger = factory.CreateLogger(typeof(T).FullName);

    public void Log(LogLevel level, string message, Exception? exception = null)
    {
        _logger.Log(level, message, exception);
    }
}
