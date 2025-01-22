namespace PicoHex.Logger.Abstractions;

public interface ILogger
{
    void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter
    );
    bool IsEnabled(LogLevel logLevel);
    IDisposable? BeginScope<TState>(TState state)
        where TState : notnull;
}
