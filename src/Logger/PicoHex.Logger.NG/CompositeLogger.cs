namespace PicoHex.Logger.NG;

public class CompositeLogger(IEnumerable<ILogger> loggers) : ILogger
{
    public void Log(LogLevel level, string message, Exception? exception = null)
    {
        foreach (var logger in loggers)
        {
            logger.Log(level, message, exception);
        }
    }

    public async ValueTask LogAsync(
        LogLevel level,
        string message,
        Exception? exception = null,
        CancellationToken cancellationToken = default
    )
    {
        foreach (var logger in loggers)
        {
            await logger.LogAsync(level, message, exception, cancellationToken);
        }
    }
}
