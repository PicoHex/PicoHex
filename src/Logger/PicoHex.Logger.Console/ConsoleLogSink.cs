namespace PicoHex.Logger.Console;

public class ConsoleLogSink : ILogSink
{
    private static readonly Lock Lock = new();

    public void Write(string formattedMessage)
    {
        lock (Lock)
        {
            var originalColor = System.Console.ForegroundColor;
            var logLevel = GetLogLevelFromMessage(formattedMessage);
            System.Console.ForegroundColor = GetConsoleColor(logLevel);
            System.Console.WriteLine(formattedMessage);
            System.Console.ForegroundColor = originalColor;
        }
    }

    public ValueTask WriteAsync(
        string formattedMessage,
        CancellationToken cancellationToken = default
    )
    {
        Write(formattedMessage);
        return ValueTask.CompletedTask;
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private static LogLevel GetLogLevelFromMessage(string message)
    {
        var levelStr = message.Substring(26, 7).TrimEnd();
        return Enum.Parse<LogLevel>(levelStr, ignoreCase: true);
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

    public void Dispose()
    {
        // TODO release managed resources here
    }
}
