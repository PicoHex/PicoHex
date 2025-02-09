namespace PicoHex.Logger.NG;

public class LogEntry
{
    public DateTime Timestamp { get; set; }
    public LogLevel Level { get; set; }
    public string? Category { get; set; }
    public string? Message { get; set; }
    public Exception? Exception { get; set; }
}
