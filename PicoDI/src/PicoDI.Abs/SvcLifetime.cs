namespace PicoDI.Abs;

/// <summary>
/// Specifies the lifetime of a service in the dependency injection container.
/// </summary>
public enum SvcLifetime : byte
{
    /// <summary>
    /// A new instance is created every time the service is requested.
    /// </summary>
    Transient,

    /// <summary>
    /// A single instance is shared across all requests within the application.
    /// </summary>
    Singleton,

    /// <summary>
    /// A single instance is created and shared within a scope.
    /// </summary>
    Scoped
}
