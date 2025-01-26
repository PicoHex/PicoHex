namespace PicoHex.Logger.Abstractions;

public interface ILogger
{
    void Log<TState>(
        LogLevel logLevel,
        LogId logId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter
    );

    ValueTask LogAsync<TState>(
        LogLevel logLevel,
        LogId logId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter
    );

    bool IsEnabled(LogLevel logLevel);
    IDisposable? BeginScope<TState>(TState state)
        where TState : notnull;
}

public interface ILogger<out T> : ILogger;
