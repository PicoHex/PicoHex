namespace PicoDI.Abs;

/// <summary>
/// Represents a dependency injection container that manages service registrations and scope creation.
/// </summary>
public interface ISvcContainer : IAsyncDisposable
{
    /// <summary>
    /// Registers a service descriptor with the container.
    /// </summary>
    /// <param name="descriptor">The service descriptor containing service type, factory, and lifetime information.</param>
    /// <returns>The container instance for method chaining.</returns>
    public ISvcContainer Register(SvcDescriptor descriptor);

    /// <summary>
    /// Creates a new service scope for resolving scoped services.
    /// </summary>
    /// <returns>A new service scope instance.</returns>
    public ISvcScope CreateScope();
}
