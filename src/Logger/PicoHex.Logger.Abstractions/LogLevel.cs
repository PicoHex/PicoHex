namespace PicoHex.Logger.Abstractions;

/// <summary>
/// Represents the severity levels of log events.
/// The underlying type is byte for compact storage and efficient serialization.
/// </summary>
public enum LogLevel : byte
{
    /// <summary>
    /// The most detailed level, used for tracing specific code paths (e.g., entering/exiting methods).
    /// Typically disabled in production environments.
    /// </summary>
    Trace,

    /// <summary>
    /// Detailed debugging information for development purposes.
    /// More granular than Information level.
    /// </summary>
    Debug,

    /// <summary>
    /// General operational entries that describe normal application flow.
    /// Example: "User logged in", "Request completed".
    /// </summary>
    Information,

    /// <summary>
    /// Significant but non-error conditions that require attention.
    /// More important than Information but less urgent than Warning.
    /// Example: "Disk space below 20%".
    /// </summary>
    Notice,

    /// <summary>
    /// Indicates potentially harmful situations that don't prevent normal operation.
    /// Example: "Invalid API request format", "Temporary network latency".
    /// </summary>
    Warning,

    /// <summary>
    /// Runtime errors that affect specific operations but allow the application to continue.
    /// Example: "Database connection failed", "File not found".
    /// </summary>
    Error,

    /// <summary>
    /// Severe failures in key application functions that may disable specific features.
    /// Example: "Payment processing failure", "Critical subsystem unavailable".
    /// </summary>
    Critical,

    /// <summary>
    /// Requires immediate human intervention to prevent system failure.
    /// Reserved for catastrophic events.
    /// Example: "Security breach detected", "Primary storage full".
    /// </summary>
    Alert,

    /// <summary>
    /// The system is completely unusable and requires urgent maintenance.
    /// Highest priority level.
    /// Example: "Server room flooding", "Complete network outage".
    /// </summary>
    Emergency,

    /// <summary>
    /// Special value indicating no logging should occur.
    /// Uses byte.MaxValue (255) to ensure it doesn't conflict with valid severity levels.
    /// </summary>
    None = byte.MaxValue
}
