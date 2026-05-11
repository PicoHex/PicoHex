namespace PicoDI.Abs;

/// <summary>
/// Represents a host that manages the lifecycle of hosted services within a dependency injection container.
/// </summary>
public interface IHost : IAsyncDisposable
{
    /// <summary>
    /// Gets the underlying service container for service resolution.
    /// </summary>
    public ISvcContainer Services { get; }

    /// <summary>
    /// Starts all registered hosted services.
    /// </summary>
    public Task StartAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Stops all registered hosted services in reverse registration order.
    /// </summary>
    public Task StopAsync(CancellationToken cancellationToken = default);
}
