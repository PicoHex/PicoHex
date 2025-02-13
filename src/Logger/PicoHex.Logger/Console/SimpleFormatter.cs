namespace PicoHex.Logger.Console;

public class SimpleFormatter : ILogFormatter
{
    public string Format(LogEntry entry) =>
        $"{entry.Timestamp:yyyy-MM-dd HH:mm:ss} [{entry.Level}] {entry.Category}: {entry.Message}";
}
