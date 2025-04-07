namespace PicoHex.DI;

public sealed class SvcScope(ISvcContainer container, ISvcResolver resolver) : ISvcScope
{
    private readonly ConcurrentDictionary<Type, object> _scopedServices = new();
    private volatile bool _disposed;

    public object Resolve(Type serviceType)
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(SvcScope));

        var descriptor =
            container.GetDescriptor(serviceType)
            ?? throw new InvalidOperationException($"Service {serviceType} not registered.");

        return descriptor.Lifetime switch
        {
            SvcLifetime.Scoped
                => _scopedServices.GetOrAdd(serviceType, resolver.Resolve(serviceType)),
            _ => resolver.Resolve(serviceType)
        };
    }

    public void Dispose() => Dispose(disposing: true);

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
            foreach (var service in _scopedServices)
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
        foreach (var service in _scopedServices)
        {
            if (service.Value is IDisposable disposable)
                disposable.Dispose();
            if (service.Value is IAsyncDisposable asyncDisposable)
                await asyncDisposable.DisposeAsync().ConfigureAwait(false);
        }
        _disposed = true;
    }
}
