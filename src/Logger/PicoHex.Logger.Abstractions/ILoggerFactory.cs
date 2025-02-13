namespace PicoHex.Logger.Abstractions;

/// <summary>
/// Represents a factory for creating and managing logger instances and their providers.
/// </summary>
/// <remarks>
/// <para>
/// Acts as the central configuration point for the logging system. This interface enables:
/// <list type="bullet">
/// <item>Logger instance creation for specific categories or types</item>
/// <item>Registration of logging providers that define output destinations</item>
/// <item>Coordination of multiple logger providers</item>
/// </list>
/// </para>
/// <para>
/// Typically configured during application startup and used throughout the application's lifetime.
/// </para>
/// </remarks>
public interface ILoggerFactory
{
    /// <summary>
    /// Creates a logger instance with the specified category name.
    /// </summary>
    /// <param name="category">The category name for messages produced by the logger</param>
    /// <returns>An <see cref="ILogger"/> instance for the specified category</returns>
    /// <remarks>
    /// <para>
    /// Category names are typically hierarchical and case-insensitive. Common patterns include:
    /// <list type="bullet">
    /// <item>Fully qualified type names (e.g., "MyApp.Services.OrderService")</item>
    /// <item>Functional categories (e.g., "Authentication", "Database")</item>
    /// </list>
    /// </para>
    /// <para>
    /// Implementations may cache and reuse logger instances for the same category.
    /// </para>
    /// </remarks>
    ILogger CreateLogger(string category);

    /// <summary>
    /// Creates a generic logger instance for the specified type.
    /// </summary>
    /// <typeparam name="T">The type whose name will be used as the logger category</typeparam>
    /// <returns>An <see cref="ILogger{T}"/> instance for the specified type</returns>
    /// <remarks>
    /// <para>
    /// This is a convenience method that typically calls <see cref="CreateLogger(string)"/> with
    /// the type's full name as the category.
    /// </para>
    /// <example>
    /// Creating a logger for a service class:
    /// <code>var logger = factory.CreateLogger&lt;OrderService&gt;();</code>
    /// </example>
    /// </remarks>
    ILogger<T> CreateLogger<T>();

    /// <summary>
    /// Adds a logging provider to the factory's collection.
    /// </summary>
    /// <param name="provider">The logging provider to add</param>
    /// <remarks>
    /// <para>
    /// Providers added through this method will be used by all subsequently created loggers.
    /// </para>
    /// <para>
    /// Typical implementations:
    /// <list type="bullet">
    /// <item>Maintain providers in a thread-safe collection</item>
    /// <item>Apply providers to new logger instances</item>
    /// <item>May process existing loggers depending on implementation strategy</item>
    /// </list>
    /// </para>
    /// <para>
    /// Provider order may affect logging behavior in implementations that support multiple providers.
    /// </para>
    /// </remarks>
    void AddProvider(ILoggerProvider provider);
}
