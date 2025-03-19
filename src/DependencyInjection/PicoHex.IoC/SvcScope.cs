namespace PicoHex.IoC;

public class SvcScope(ISvcProvider provider) : ISvcScope
{
    private readonly IList<object?> _services = new List<object?>();
    private volatile bool _disposed;

    public object? Resolve(Type serviceType)
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(SvcScope));
        var service = provider.Resolve(serviceType);
        _services.Add(service);
        return service;
    }

    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    public async ValueTask DisposeAsync()
    {
        await DisposeAsyncCore().ConfigureAwait(false);
        Dispose(disposing: false);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (_disposed)
            return;
        if (disposing)
        {
            foreach (var service in _services)
            {
                if (service is IDisposable disposable)
                    disposable.Dispose();
            }
        }
        _disposed = true;
    }

    protected virtual async ValueTask DisposeAsyncCore()
    {
        if (_disposed)
            return;
        foreach (var service in _services)
        {
            if (service is IDisposable disposable)
                disposable.Dispose();
            if (service is IAsyncDisposable asyncDisposable)
                await asyncDisposable.DisposeAsync().ConfigureAwait(false);
        }
        _disposed = true;
    }
}
