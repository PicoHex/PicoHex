namespace PicoHex.Logger.NG.Abstractions;

/// <summary>
/// Represents a single entry in the application log, containing relevant information for tracking and debugging.
/// </summary>
/// <remarks>
/// This class serves as the fundamental unit of logging within the framework, capturing essential details
/// about application events, errors, and diagnostic information.
/// </remarks>
public class LogEntry
{
    /// <summary>
    /// Gets or sets the exact date and time when the log entry was created.
    /// </summary>
    /// <value>
    /// UTC timestamp of the log event, typically initialized automatically when the entry is created.
    /// </value>
    public DateTime Timestamp { get; set; }

    /// <summary>
    /// Gets or sets the severity level of the log entry.
    /// </summary>
    /// <value>
    /// One of the <see cref="LogLevel"/> values indicating the importance/severity category
    /// of the log entry (e.g., Debug, Information, Warning, Error, Critical).
    /// </value>
    public LogLevel Level { get; set; }

    /// <summary>
    /// Gets or sets the category name for the log entry.
    /// </summary>
    /// <value>
    /// Optional string typically used to indicate the source component or service type
    /// generating the log (e.g., controller name, service class name).
    /// </value>
    public string? Category { get; set; }

    /// <summary>
    /// Gets or sets the primary log message describing the event.
    /// </summary>
    /// <value>
    /// Human-readable message describing the logged event. This is the main content
    /// of the log entry and is typically required for meaningful logging.
    /// </value>
    public string? Message { get; set; }

    /// <summary>
    /// Gets or sets the exception associated with the log entry.
    /// </summary>
    /// <value>
    /// Optional <see cref="Exception"/> object containing error details when logging
    /// error conditions or critical failures. Includes full exception stack trace
    /// when available.
    /// </value>
    public Exception? Exception { get; set; }
}
