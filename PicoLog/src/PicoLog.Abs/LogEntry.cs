namespace PicoLog.Abs;

public sealed class LogEntry
{
    public DateTimeOffset Timestamp { get; set; }
    public LogLevel Level { get; set; }
    public string? Category { get; set; }
    public string? Message { get; set; }
    public Exception? Exception { get; set; }
    public IReadOnlyList<object>? Scopes { get; set; }
    public IReadOnlyList<KeyValuePair<string, object?>>? Properties { get; set; }
    public string? MessageTemplate { get; set; }
    public IReadOnlyList<object?>? MessageArgs { get; set; }
    public IReadOnlyList<KeyValuePair<string, object?>>? ScopeProperties { get; set; }
    public EventId EventId { get; set; }

    /// <summary>
    /// Resets all properties to default values, preparing the entry for reuse.
    /// </summary>
    public void Reset()
    {
        Timestamp = default;
        Level = default;
        Category = null;
        Message = null;
        Exception = null;
        Scopes = null;
        Properties = null;
        MessageTemplate = null;
        MessageArgs = null;
        ScopeProperties = null;
        EventId = default;
    }
}
