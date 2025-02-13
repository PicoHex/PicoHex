namespace PicoHex.Logger.Json;

public class JsonFormatter : ILogFormatter
{
    public string Format(LogEntry entry) => System.Text.Json.JsonSerializer.Serialize(entry);
}
