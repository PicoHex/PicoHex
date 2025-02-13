namespace PicoHex.Logger.Console;

public class JsonFormatter : ILogFormatter
{
    public string Format(LogEntry entry)
    {
        return System.Text.Json.JsonSerializer.Serialize(entry);
    }
}
