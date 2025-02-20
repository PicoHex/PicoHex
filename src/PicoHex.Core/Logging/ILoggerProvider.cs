namespace PicoHex.Core.Logging;

/// <summary>
/// Provides instances of <see cref="ILogger"/> for specific logging categories.
/// </summary>
/// <remarks>
/// Represents the factory pattern for creating logger instances in the logging framework.
/// Implement this interface to enable dependency injection and lifetime management of loggers.
/// Typical implementations may create new loggers or return cached instances based on the category name.
/// </remarks>
public interface ILoggerProvider : IDisposable
{
    /// <summary>
    /// Creates a new <see cref="ILogger"/> instance for the specified category.
    /// </summary>
    /// <param name="category">The category name for messages produced by the logger</param>
    /// <returns>A logger instance for the specified category</returns>
    /// <remarks>
    /// <para>
    /// The category name is typically the fully qualified name of the class producing the logs.
    /// Implementations may use this category to filter or route log messages.
    /// </para>
    /// <para>
    /// Multiple calls with the same category may return the same logger instance,
    /// depending on the provider's implementation strategy.
    /// </para>
    /// <para>
    /// Implementations should ensure thread safety when creating and returning logger instances.
    /// </para>
    /// </remarks>
    /// <example>
    /// Creating a logger for a service class:
    /// <code>var logger = provider.CreateLogger("MyApp.Services.OrderService");</code>
    /// </example>
    ILogger CreateLogger(string category);
}
