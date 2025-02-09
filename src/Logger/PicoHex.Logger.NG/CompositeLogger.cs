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
}
