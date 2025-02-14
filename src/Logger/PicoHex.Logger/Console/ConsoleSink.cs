namespace PicoHex.Logger.Console;

public class ConsoleSink : ILogSink
{
    private readonly Channel<LogEntry> _channel = Channel.CreateUnbounded<LogEntry>();
    private readonly ILogFormatter _formatter;
    private readonly Task _processingTask;
    public LogLevel MinimumLevel { get; set; } = LogLevel.Debug;

    public ConsoleSink(ILogFormatter formatter)
    {
        _formatter = formatter;
        _processingTask = Task.Run(ProcessEntries);
    }

    private async Task ProcessEntries()
    {
        await foreach (var entry in _channel.Reader.ReadAllAsync())
        {
            try
            {
                var originalColor = System.Console.ForegroundColor;
                System.Console.ForegroundColor = GetConsoleColor(entry.Level);
                System.Console.WriteLine(_formatter.Format(entry));
                System.Console.ForegroundColor = originalColor;
            }
            catch
            {
                // ignored
            }
        }
    }

    public void Emit(LogEntry entry)
    {
        if (entry.Level >= MinimumLevel)
            _channel.Writer.TryWrite(entry);
    }

    public ValueTask EmitAsync(LogEntry entry, CancellationToken cancellationToken = default) =>
        entry.Level < MinimumLevel
            ? ValueTask.CompletedTask
            : _channel.Writer.WriteAsync(entry, cancellationToken);

    private static ConsoleColor GetConsoleColor(LogLevel logLevel) =>
        logLevel switch
        {
            LogLevel.Trace => ConsoleColor.DarkGray,
            LogLevel.Debug => ConsoleColor.Gray,
            LogLevel.Information => ConsoleColor.White,
            LogLevel.Notice => ConsoleColor.Cyan,
            LogLevel.Warning => ConsoleColor.Yellow,
            LogLevel.Error => ConsoleColor.Red,
            LogLevel.Critical => ConsoleColor.DarkRed,
            LogLevel.Alert => ConsoleColor.Magenta,
            LogLevel.Emergency => ConsoleColor.DarkMagenta,
            _ => ConsoleColor.White
        };

    public void Dispose()
    {
        _channel.Writer.Complete();
        _processingTask.Wait();
    }

    public ValueTask DisposeAsync()
    {
        Dispose();
        return ValueTask.CompletedTask;
    }
}
