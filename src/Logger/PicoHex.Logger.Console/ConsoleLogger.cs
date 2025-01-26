namespace PicoHex.Logger.Console;

public class ConsoleLogger(string categoryName, ILogFormatter formatter, ILogSink sink) : ILogger
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

        var formattedMessage = formatter.Format(
            logLevel,
            categoryName,
            logId,
            state,
            exception,
            formatter1
        );

        // 同步等待异步写入完成（确保日志顺序）
        sink.Write(formattedMessage);
    }

    public ValueTask LogAsync<TState>(
        LogLevel logLevel,
        LogId logId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter1
    )
    {
        if (!IsEnabled(logLevel))
            return ValueTask.CompletedTask;

        var formattedMessage = formatter.Format(
            logLevel,
            categoryName,
            logId,
            state,
            exception,
            formatter1
        );

        // 同步等待异步写入完成（确保日志顺序）
        return sink.WriteAsync(formattedMessage);
    }

    public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None; // 默认启用所有级别

    public IDisposable? BeginScope<TState>(TState state)
        where TState : notnull => NullScope.Instance; // 暂不支持作用域
}

public sealed class ConsoleLogger<T>(ILogFormatter formatter, ILogSink sink)
    : ConsoleLogger(typeof(T).FullName ?? typeof(T).Name, formatter, sink),
        ILogger<T>;
