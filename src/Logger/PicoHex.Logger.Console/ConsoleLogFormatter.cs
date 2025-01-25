namespace PicoHex.Logger.Console;

public class ConsoleLogFormatter : ILogFormatter
{
    public string Format<TState>(
        LogLevel logLevel,
        string categoryName,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> defaultFormatter
    )
    {
        var timestamp = DateTimeOffset.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
        var logLevelStr = logLevel.ToString().ToUpperInvariant().PadRight(7);
        var message = defaultFormatter(state, exception);
        var exceptionStr = exception == null ? "" : $"\n{exception}";

        return $"[{timestamp}] [{logLevelStr}] [{categoryName}] {message}{exceptionStr}";
    }
}
