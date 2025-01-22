namespace PicoHex.Logger;

public class JsonLogFormatter : ILogFormatter
{
    public string Format<TState>(
        LogLevel logLevel,
        string categoryName,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> defaultFormatter
    )
    {
        var logEntry = new
        {
            Timestamp = DateTime.UtcNow,
            Level = logLevel.ToString(),
            Category = categoryName,
            EventId = eventId,
            Message = defaultFormatter(state, exception),
            Exception = exception?.ToString()
        };
        return JsonConvert.SerializeObject(logEntry);
    }
}

// 文件输出 Sink
public class FileLogSink : ILogSink, IDisposable
{
    private readonly StreamWriter _writer;

    public FileLogSink(string filePath)
    {
        _writer = new StreamWriter(filePath, append: true);
    }

    public Task WriteAsync(string formattedMessage)
    {
        return _writer.WriteLineAsync(formattedMessage);
    }

    public void Dispose()
    {
        _writer.Dispose();
    }
}
