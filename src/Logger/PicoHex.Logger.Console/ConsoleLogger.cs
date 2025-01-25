namespace PicoHex.Logger.Console;

public class ConsoleLogger : ILogger
{
    private readonly string _categoryName;
    private readonly ILogFormatter _formatter;
    private readonly ILogSink _sink;

    public ConsoleLogger(string categoryName, ILogFormatter formatter, ILogSink sink)
    {
        _categoryName = categoryName;
        _formatter = formatter;
        _sink = sink;
    }

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter
    )
    {
        if (!IsEnabled(logLevel))
            return;

        var formattedMessage = _formatter.Format(
            logLevel,
            _categoryName,
            eventId,
            state,
            exception,
            formatter
        );

        // 同步等待异步写入完成（确保日志顺序）
        _sink.WriteAsync(formattedMessage).GetAwaiter().GetResult();
    }

    public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None; // 默认启用所有级别

    public IDisposable? BeginScope<TState>(TState state)
        where TState : notnull => NullScope.Instance; // 暂不支持作用域
}

public sealed class ConsoleLogger<T> : ConsoleLogger, ILogger<T>
{
    public ConsoleLogger(ILogFormatter formatter, ILogSink sink)
        : base(typeof(T).FullName ?? typeof(T).Name, formatter, sink) { }
}
