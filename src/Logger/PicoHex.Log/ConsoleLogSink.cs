namespace PicoHex.Log;

public class ConsoleLogSink : ILogSink
{
    private readonly Channel<LogEntry> _channel = Channel.CreateUnbounded<LogEntry>();
    private readonly ILogFormatter _formatter;
    private readonly Task _processingTask;

    public ConsoleLogSink(ILogFormatter formatter)
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
                var formatted = _formatter.Format(entry);
                WriteColoredLog(entry.Level, formatted);
            }
            catch
            { /* 确保主线程不受影响 */
            }
        }
    }

    public async ValueTask WriteAsync(
        LogEntry entry,
        CancellationToken cancellationToken = default
    ) => await _channel.Writer.WriteAsync(entry, cancellationToken);

    private void WriteColoredLog(LogLevel level, string message)
    {
        var originalColor = Console.ForegroundColor;

        // 根据日志级别设置颜色
        Console.ForegroundColor = level switch
        {
            LogLevel.Trace => ConsoleColor.Gray,
            LogLevel.Debug => ConsoleColor.Cyan,
            LogLevel.Info => ConsoleColor.Green,
            LogLevel.Notice => ConsoleColor.Blue,
            LogLevel.Warning => ConsoleColor.Yellow,
            LogLevel.Error => ConsoleColor.Red,
            LogLevel.Critical => ConsoleColor.DarkRed,
            LogLevel.Alert => ConsoleColor.Magenta,
            LogLevel.Emergency => ConsoleColor.DarkMagenta,
            LogLevel.None => originalColor,
            _ => originalColor
        };

        Console.WriteLine(message);
        Console.ForegroundColor = originalColor; // 恢复原始颜色
    }

    public void Dispose()
    {
        _channel.Writer.Complete();
        _processingTask.Wait();
    }
}
