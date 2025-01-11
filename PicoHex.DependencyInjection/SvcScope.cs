namespace PicoHex.DependencyInjection;

public class SvcScope(ISvcProvider provider) : ISvcScope
{
    private readonly ConcurrentDictionary<Type, object> _services = new();

    public object Resolve(Type implementationType) =>
        _services.GetOrAdd(
            implementationType,
            provider.Resolve(implementationType)
                ?? throw new InvalidOperationException(
                    $"Service for type {implementationType} could not be resolved."
                )
        );

    public void Dispose()
    {
        foreach (var svc in _services.Values.OfType<IDisposable>())
            svc.Dispose();
        _services.Clear();
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var svc in _services.Values.OfType<IAsyncDisposable>())
            await svc.DisposeAsync();
        Dispose();
    }
}
