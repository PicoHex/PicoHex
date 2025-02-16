namespace PicoHex.Logger;

internal class CategoryLogger(string category, ILogSink sink) : ILogger
{
    public void Log(LogLevel level, string message, Exception? exception = null)
    {
        if (level < sink.MinimumLevel)
            return;

        var entry = new LogEntry(DateTime.Now, level, category, message, exception);

        sink.Emit(entry);
    }

    public async ValueTask LogAsync(
        LogLevel level,
        string message,
        Exception? exception = null,
        CancellationToken cancellationToken = default
    )
    {
        if (level < sink.MinimumLevel)
            return;

        var entry = new LogEntry(DateTime.Now, level, category, message, exception);

        await sink.EmitAsync(entry, cancellationToken);
    }

    public IDisposable BeginScope<TState>(TState state)
    {
        return new LogScope(state);
    }
}
