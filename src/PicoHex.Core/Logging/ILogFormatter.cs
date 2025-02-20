namespace PicoHex.Core.Logging;

/// <summary>
/// Defines a formatter for converting log entries into standardized string representations.
/// </summary>
/// <remarks>
/// <para>
/// Implement this interface to create custom formatting rules for log entries. Typical use cases include:
/// <list type="bullet">
/// <item>Structured logging formats (JSON, XML, etc.)</item>
/// <item>Human-readable console output</item>
/// <item>Specialized file formatting requirements</item>
/// <item>Protocol-specific formats for external systems</item>
/// </list>
/// </para>
/// <para>
/// Formatters should be thread-safe and stateless where possible to enable concurrent logging operations.
/// </para>
/// </remarks>
public interface ILogFormatter
{
    /// <summary>
    /// Converts a log entry into a formatted string representation.
    /// </summary>
    /// <param name="entry">The log entry to format</param>
    /// <returns>A formatted string ready for output</returns>
    /// <remarks>
    /// <para>
    /// Implementations should:
    /// <list type="bullet">
    /// <item>Handle all properties of the <see cref="LogEntry"/> including optional values</item>
    /// <item>Format exceptions with stack traces when present</item>
    /// <item>Ensure proper escaping for the target output medium</item>
    /// <item>Maintain consistent performance characteristics</item>
    /// </list>
    /// </para>
    /// <para>
    /// Example formatting elements:
    /// <code>
    /// [{entry.Timestamp:O}] [{entry.Level}] {entry.Category}: {entry.Message}
    /// </code>
    /// </para>
    /// <para>
    /// This method should never throw exceptions. Formatting errors should be handled gracefully,
    /// potentially returning fallback text or empty strings.
    /// </para>
    /// </remarks>
    string Format(LogEntry entry);
}
