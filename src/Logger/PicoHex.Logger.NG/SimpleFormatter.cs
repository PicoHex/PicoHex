namespace PicoHex.Logger.NG;

public class SimpleFormatter : ILogFormatter
{
    public string Format(LogEntry entry)
    {
        return $"{entry.Timestamp:yyyy-MM-dd HH:mm:ss} [{entry.Level}] {entry.Category}: {entry.Message}";
    }
}
