namespace PicoHex.Logger;

public class CustomLoggerProvider(ILogFormatter formatter, ILogSink sink) : ILoggerProvider
{
    public ILogger CreateLogger(string categoryName)
    {
        return new CustomLogger(categoryName, formatter, sink);
    }

    public void Dispose() => sink.Dispose();
}

// 具体的 Logger 实现
public class CustomLogger(string categoryName, ILogFormatter formatter, ILogSink sink) : ILogger
{
    public void Log<TState>(
        LogLevel logLevel,
        LogId logId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter1
    )
    {
        if (!IsEnabled(logLevel))
            return;

        // 使用自定义格式化器
        var formattedMessage = formatter.Format(
            logLevel,
            categoryName,
            logId,
            state,
            exception,
            formatter1
        );

        // 输出到 Sink
        sink.WriteAsync(formattedMessage).ConfigureAwait(false).GetAwaiter().GetResult();
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

    public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Information;

    public IDisposable BeginScope<TState>(TState state) => NullScope.Instance;
}

// 空 Scope 实现
public class NullScope : IDisposable
{
    public static NullScope Instance { get; } = new NullScope();

    public void Dispose() { }
}
