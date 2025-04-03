namespace PicoHex.Log;

public sealed class ConsoleLogSink(ILogFormatter formatter) : ILogSink
{
    public ValueTask WriteAsync(LogEntry entry, CancellationToken cancellationToken = default)
    {
        WriteColoredLog(entry.Level, formatter.Format(entry));
        return ValueTask.CompletedTask;
    }

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

    public void Dispose() { }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
