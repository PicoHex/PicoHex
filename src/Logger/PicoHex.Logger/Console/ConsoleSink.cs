namespace PicoHex.Logger.Console;

public class ConsoleSink(ILogFormatter formatter) : ILogSink
{
    public LogLevel MinimumLevel { get; set; } = LogLevel.Debug;

    public void Emit(LogEntry entry)
    {
        if (entry.Level < MinimumLevel)
            return;

        var originalColor = System.Console.ForegroundColor;
        System.Console.ForegroundColor = GetConsoleColor(entry.Level);
        System.Console.WriteLine(formatter.Format(entry));
        System.Console.ForegroundColor = originalColor;
    }

    public ValueTask EmitAsync(LogEntry entry, CancellationToken cancellationToken = default)
    {
        Emit(entry);
        return ValueTask.CompletedTask;
    }

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
}
