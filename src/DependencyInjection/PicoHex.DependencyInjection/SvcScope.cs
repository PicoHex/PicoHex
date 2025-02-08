namespace PicoHex.DependencyInjection;

public sealed class SvcScope(ISvcProvider provider) : ISvcScope
{
    private readonly ISvcProvider _provider =
        provider ?? throw new ArgumentNullException(nameof(provider));
    private readonly ConcurrentDictionary<Type, object?> _services = new();
    private volatile bool _disposed;

    public object? Resolve(Type serviceType) =>
        _disposed
            ? throw new ObjectDisposedException(nameof(SvcScope))
            : _services.GetOrAdd(serviceType, _provider.Resolve);

    public void Dispose()
    {
        if (_disposed)
            return;

        foreach (var svc in _services.Values)
        {
            switch (svc)
            {
                case IDisposable disposable:
                    try
                    {
                        disposable.Dispose();
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"同步释放 {svc.GetType()} 失败: {ex}");
                    }
                    break;

                case IAsyncDisposable asyncDisposable:
                    try
                    {
                        asyncDisposable.DisposeAsync().GetAwaiter().GetResult();
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"强制同步释放异步资源 {svc.GetType()} 失败: {ex}");
                    }
                    break;
            }
        }

        _services.Clear();
        _disposed = true;
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;

        foreach (var svc in _services.Values.OfType<IAsyncDisposable>())
        {
            try
            {
                await svc.DisposeAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"异步释放 {svc.GetType()} 失败: {ex}");
            }
        }

        foreach (var svc in _services.Values.OfType<IDisposable>())
        {
            if (svc is IAsyncDisposable)
                continue;

            try
            {
                svc.Dispose();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"异步路径同步释放 {svc.GetType()} 失败: {ex}");
            }
        }

        _services.Clear();
        _disposed = true;
    }
}
