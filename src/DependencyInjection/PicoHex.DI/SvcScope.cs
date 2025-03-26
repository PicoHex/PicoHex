namespace PicoHex.DI;

public sealed class SvcScope(ISvcProvider provider) : ISvcScope
{
    private readonly ConcurrentDictionary<Type, object?> _services = new();
    private volatile bool _disposed;

    public object? Resolve(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
            Type serviceType
    ) =>
        _disposed
            ? throw new ObjectDisposedException(nameof(SvcScope))
            : _services.GetOrAdd(serviceType, provider.Resolve);

    public void Dispose()
    {
        Dispose(disposing: true);
    }

    public async ValueTask DisposeAsync()
    {
        await DisposeAsyncCore().ConfigureAwait(false);
        Dispose(disposing: false);
    }

    private void Dispose(bool disposing)
    {
        if (_disposed)
            return;
        if (disposing)
        {
            foreach (var service in _services)
            {
                if (service.Value is IDisposable disposable)
                    disposable.Dispose();
            }
        }
        _disposed = true;
    }

    private async ValueTask DisposeAsyncCore()
    {
        if (_disposed)
            return;
        foreach (var service in _services)
        {
            if (service.Value is IDisposable disposable)
                disposable.Dispose();
            if (service.Value is IAsyncDisposable asyncDisposable)
                await asyncDisposable.DisposeAsync().ConfigureAwait(false);
        }
        _disposed = true;
    }
}
