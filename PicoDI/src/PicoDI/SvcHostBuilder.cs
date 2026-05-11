namespace PicoDI;

/// <summary>
/// Fluent builder for creating and starting an <see cref="IHost"/>
/// with hosted service support.
/// </summary>
public sealed class SvcHostBuilder : IAsyncDisposable
{
    private readonly List<Action<ISvcContainer>> _configureActions = [];
    private IHost? _host;
    private bool _built;

    /// <summary>
    /// Adds a configuration delegate to customize the container's service registrations.
    /// Multiple calls accumulate — all delegates are applied in order during <see cref="Build"/>.
    /// </summary>
    public SvcHostBuilder ConfigureServices(Action<ISvcContainer> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        if (_built)
            throw new InvalidOperationException(
                "Cannot call ConfigureServices after BuildAsync has been called."
            );
        _configureActions.Add(configure);
        return this;
    }

    /// <summary>
    /// Creates the container asynchronously.
    /// </summary>
    public async Task<IHost> BuildAsync(CancellationToken cancellationToken = default)
    {
        if (_built)
            throw new InvalidOperationException("Build has already been called.");
        _built = true;

        var container = new SvcContainer();

        try
        {
            foreach (var action in _configureActions)
                action(container);

            container.Build();
            var host = new SvcHost(container);
            await host.StartAsync(cancellationToken).ConfigureAwait(false);

            _host = host;
            return host;
        }
        catch
        {
            await container.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    public async ValueTask DisposeAsync()
    {
        var host = Interlocked.Exchange(ref _host, null);
        if (host is not null)
            await host.DisposeAsync().ConfigureAwait(false);
    }
}
