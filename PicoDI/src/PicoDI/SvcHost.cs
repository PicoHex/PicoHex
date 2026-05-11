namespace PicoDI;

/// <summary>
/// Default implementation of <see cref="IHost"/> that wraps an <see cref="SvcContainer"/>.
/// </summary>
public sealed class SvcHost(SvcContainer container) : IHost
{
    /// <inheritdoc />
    public ISvcContainer Services => container;

    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken = default) =>
        container.StartHostedServicesAsync(cancellationToken);

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken = default) =>
        container.StopHostedServicesAsync(cancellationToken);

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        await container.DisposeAsync().ConfigureAwait(false);
    }
}
