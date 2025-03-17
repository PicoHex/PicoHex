namespace PicoHex.Core.Logging;

public record LogEntry(
    DateTime Timestamp,
    LogLevel Level,
    string? Category = null,
    string? Message = null,
    Exception? Exception = null,
    IReadOnlyList<KeyValuePair<string, object>>? Scopes = null
)
{
    public DateTime Timestamp { get; } = Timestamp;

    public LogLevel Level { get; } = Level;

    public string? Category { get; } = Category;

    public string? Message { get; } = Message;

    public Exception? Exception { get; } = Exception;

    public IReadOnlyList<KeyValuePair<string, object>>? Scopes { get; } = Scopes;
}
