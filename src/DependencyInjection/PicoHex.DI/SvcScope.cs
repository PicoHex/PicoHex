namespace PicoHex.DI;

public sealed class SvcScope(ISvcContainer container, ISvcResolver resolver) : ISvcScope
{
    private readonly ConcurrentDictionary<Type, object> _scopedInstances = new();
    private volatile bool _disposed;

    public object Resolve(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
            Type serviceType
    )
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(SvcScope));

        var descriptor =
            container.GetDescriptor(serviceType)
            ?? throw new InvalidOperationException($"Service {serviceType} not registered.");

        return descriptor.Lifetime switch
        {
            SvcLifetime.Scoped
                => _scopedInstances.GetOrAdd(
                    serviceType,
                    new Lazy<object>(
                        () => resolver.Resolve(serviceType),
                        LazyThreadSafetyMode.ExecutionAndPublication
                    ).Value
                ),
            _ => resolver.Resolve(serviceType)
        };
    }

    public void Dispose() => DisposeCore(disposing: true);

    public async ValueTask DisposeAsync()
    {
        await DisposeAsyncCore().ConfigureAwait(false);
        DisposeCore(disposing: false);
    }

    private void DisposeCore(bool disposing)
    {
        if (_disposed)
            return;

        if (disposing)
            DisposeInstances(instance => (instance as IDisposable)?.Dispose());
        _disposed = true;
    }

    private async ValueTask DisposeAsyncCore()
    {
        if (_disposed)
            return;

        foreach (var instance in _scopedInstances.Values)
        {
            if (instance is IAsyncDisposable asyncDisposable)
                await asyncDisposable.DisposeAsync().ConfigureAwait(false);
            (instance as IDisposable)?.Dispose();
        }
        _disposed = true;
    }

    private void DisposeInstances(Action<object> disposeAction)
    {
        foreach (var instance in _scopedInstances.Values)
            disposeAction(instance);
    }
}
