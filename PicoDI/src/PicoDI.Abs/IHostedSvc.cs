namespace PicoDI.Abs;

/// <summary>
/// Defines a hosted service that can be started and stopped asynchronously.
/// </summary>
public interface IHostedSvc
{
    /// <summary>
    /// Initiates the asynchronous startup of the hosted service.
    /// </summary>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the startup operation.</param>
    /// <returns>A task that represents the asynchronous startup operation.</returns>
    public Task StartAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Initiates the asynchronous shutdown of the hosted service.
    /// </summary>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the shutdown operation.</param>
    /// <returns>A task that represents the asynchronous shutdown operation.</returns>
    public Task StopAsync(CancellationToken cancellationToken);
}
