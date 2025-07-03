namespace Pico.IoC;

public sealed class SvcScope : ISvcScope
{
    private readonly ConcurrentDictionary<Type, object> _scopedInstances = new();
    private readonly ISvcContainer _container;
    private readonly ISvcProvider _provider;
    private readonly ISvcResolver _resolver;

    private volatile bool _disposed;

    public SvcScope(
        ISvcContainer container,
        ISvcProvider provider,
        ISvcResolverFactory resolverFactory
    )
    {
        _container = container;
        _provider = provider;
        _resolver = resolverFactory.CreateResolver(container, this);
    }

    public object Resolve(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
            Type serviceType
    )
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(SvcScope));

        var descriptor = _container.GetDescriptor(serviceType);

        return descriptor.Lifetime switch
        {
            SvcLifetime.Singleton => _provider.Resolve(serviceType),
            SvcLifetime.Scoped
                => _scopedInstances.GetOrAdd(
                    serviceType,
                    _ =>
                        new Lazy<object>(
                            () => _resolver.Resolve(serviceType),
                            LazyThreadSafetyMode.ExecutionAndPublication
                        ).Value
                ),
            _ => _resolver.Resolve(serviceType)
        };
    }

    public ISvcScope CreateScope() => _provider.CreateScope();

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
