namespace PicoHex.Logger.Abstractions;

public interface ILogFormatter
{
    string Format<TState>(
        LogLevel logLevel,
        string categoryName,
        LogId logId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> defaultFormatter
    );
}
