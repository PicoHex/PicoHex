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
        return System.Text.Json.JsonSerializer.Serialize(logEntry);
    }
}

// 文件输出 Sink
public class FileLogSink(string filePath) : ILogSink, IDisposable
{
    private readonly StreamWriter _writer = new(filePath, append: true);

    public async ValueTask WriteAsync(string formattedMessage)
    {
        await _writer.WriteLineAsync(formattedMessage);
    }

    public void Dispose()
    {
        _writer.Dispose();
    }

    public async ValueTask DisposeAsync()
    {
        await _writer.DisposeAsync();
    }
}
