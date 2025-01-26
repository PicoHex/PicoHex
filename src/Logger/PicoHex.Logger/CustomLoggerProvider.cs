namespace PicoHex.Logger;

public class CustomLoggerProvider : ILoggerProvider
{
    private readonly ILogFormatter _formatter;
    private readonly ILogSink _sink;

    public CustomLoggerProvider(ILogFormatter formatter, ILogSink sink)
    {
        _formatter = formatter;
        _sink = sink;
    }

    public ILogger CreateLogger(string categoryName)
    {
        return new CustomLogger(categoryName, _formatter, _sink);
    }

    public void Dispose() => _sink.Dispose();
}

// 具体的 Logger 实现
public class CustomLogger : ILogger
{
    private readonly string _categoryName;
    private readonly ILogFormatter _formatter;
    private readonly ILogSink _sink;

    public CustomLogger(string categoryName, ILogFormatter formatter, ILogSink sink)
    {
        _categoryName = categoryName;
        _formatter = formatter;
        _sink = sink;
    }

    public void Log<TState>(
        LogLevel logLevel,
        LogId logId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter
    )
    {
        if (!IsEnabled(logLevel))
            return;

        // 使用自定义格式化器
        var formattedMessage = _formatter.Format(
            logLevel,
            _categoryName,
            logId,
            state,
            exception,
            formatter
        );

        // 输出到 Sink
        _sink.WriteAsync(formattedMessage).ConfigureAwait(false).GetAwaiter().GetResult();
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
