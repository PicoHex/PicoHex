namespace PicoLog.Abs;

[AttributeUsage(AttributeTargets.Method)]
public sealed class PicoLogMessageAttribute : Attribute
{
    public LogLevel Level { get; }
    public int EventId { get; set; }
    public string? EventName { get; set; }
    public string? Message { get; set; }

    public PicoLogMessageAttribute(LogLevel level) => Level = level;
}
