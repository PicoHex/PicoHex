namespace PicoHex.Logger.Abstractions;

public interface ILogFormatter
{
    string Format<TState>(
        LogLevel logLevel,
        string categoryName,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> defaultFormatter
    );
}
