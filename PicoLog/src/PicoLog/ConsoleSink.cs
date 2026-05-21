namespace PicoLog;

public sealed class ConsoleSink(ILogFormatter formatter, TextWriter? writer = null)
    : IConsoleFallbackSink
{
    private readonly ILogFormatter _formatter =
        formatter ?? throw new ArgumentNullException(nameof(formatter));
    private readonly TextWriter _writer = writer ?? Console.Out;

    public Task WriteAsync(LogEntry entry, CancellationToken cancellationToken = default)
    {
        var message = _formatter.Format(entry);
        ConsoleSinkWriter.Write(_writer, message);
        return Task.CompletedTask;
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
