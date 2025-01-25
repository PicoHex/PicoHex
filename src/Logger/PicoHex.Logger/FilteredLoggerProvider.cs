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

    public void Dispose() => innerProvider.Dispose();
}

public class FilteredLogger : ILogger
{
    private readonly ILogger _innerLogger;
    private readonly Func<LogLevel, bool> _filter;

    public FilteredLogger(ILogger innerLogger, Func<LogLevel, bool> filter)
    {
        _innerLogger = innerLogger;
        _filter = filter;
    }

    public void Log<TState>(
        LogLevel logLevel,
        EventId<> eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter
    )
    {
        if (_filter(logLevel))
        {
            _innerLogger.Log(logLevel, eventId, state, exception, formatter);
        }
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
