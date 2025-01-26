namespace PicoHex.Logger;

public class FilteredLoggerProvider(
    ILoggerProvider innerProvider,
    Func<string, LogLevel, bool> filter
) : ILoggerProvider
{
    public ILogger CreateLogger(string categoryName)
    {
        var innerLogger = innerProvider.CreateLogger(categoryName);
        return new FilteredLogger(innerLogger, logLevel => filter(categoryName, logLevel));
    }
}

public class FilteredLogger(ILogger innerLogger, Func<LogLevel, bool> filter) : ILogger
{
    public void Log<TState>(
        LogLevel logLevel,
        LogId logId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter
    )
    {
        if (filter(logLevel))
        {
            innerLogger.Log(logLevel, logId, state, exception, formatter);
        }
    }

    public ValueTask LogAsync<TState>(
        LogLevel logLevel,
        LogId logId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter
    )
    {
        throw new NotImplementedException();
    }

    public bool IsEnabled(LogLevel logLevel)
    {
        throw new NotImplementedException();
    }

    public IDisposable? BeginScope<TState>(TState state)
        where TState : notnull
    {
        throw new NotImplementedException();
    }

    // 其他方法省略...
}
