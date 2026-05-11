namespace PicoDI.Abs;

/// <summary>
/// Defines a hosted service with a four-phase lifecycle: starting, started, stopping, and stopped.
/// Extends <see cref="IHostedSvc"/> with pre- and post-phase hooks for startup and shutdown.
/// </summary>
public interface IHostedLifecycleSvc : IHostedSvc
{
    /// <summary>
    /// Called before the hosted service starts, providing an opportunity to perform pre-startup initialization.
    /// </summary>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the operation.</param>
    /// <returns>A task that represents the asynchronous pre-startup operation.</returns>
    public Task StartingAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Called after the hosted service has started, providing an opportunity to perform post-startup operations.
    /// </summary>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the operation.</param>
    /// <returns>A task that represents the asynchronous post-startup operation.</returns>
    public Task StartedAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Called before the hosted service stops, providing an opportunity to perform pre-shutdown operations.
    /// </summary>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the operation.</param>
    /// <returns>A task that represents the asynchronous pre-shutdown operation.</returns>
    public Task StoppingAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Called after the hosted service has stopped, providing an opportunity to perform post-shutdown cleanup.
    /// </summary>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the operation.</param>
    /// <returns>A task that represents the asynchronous post-shutdown operation.</returns>
    public Task StoppedAsync(CancellationToken cancellationToken);
}
