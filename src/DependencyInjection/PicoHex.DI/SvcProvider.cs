namespace PicoHex.DI;

public sealed class SvcProvider(
    ISvcContainer container,
    ISvcScopeFactory scopeFactory,
    ISvcResolverFactory resolverFactory
) : ISvcProvider
{
    private readonly ConcurrentDictionary<Type, object> _singletonInstances = new();
    private readonly ISvcResolver _resolver = resolverFactory.CreateResolver(container);
    private volatile bool _disposed;

    public object Resolve(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
            Type serviceType
    )
    {
        EnsureNotDisposed();

        var descriptor = container.GetDescriptor(serviceType);

        return descriptor.Lifetime switch
        {
            SvcLifetime.Transient => _resolver.Resolve(serviceType),
            SvcLifetime.Singleton
                => _singletonInstances.GetOrAdd(
                    serviceType,
                    new Lazy<object>(
                        () => _resolver.Resolve(serviceType),
                        LazyThreadSafetyMode.ExecutionAndPublication
                    ).Value
                ),
            SvcLifetime.Scoped
                => throw new InvalidOperationException(
                    $"Can not resolve {serviceType} as Scoped from root provider."
                ),
            _ => throw new ArgumentOutOfRangeException(nameof(descriptor.Lifetime))
        };
    }

    public ISvcScope CreateScope()
    {
        EnsureNotDisposed();
        return scopeFactory.CreateScope(container, this, resolverFactory);
    }

    public void Dispose() => DisposeCore(disposing: true);

    public async ValueTask DisposeAsync()
    {
        await DisposeAsyncCore().ConfigureAwait(false);
        DisposeCore(disposing: false);
    }

    #region Private Methods

    private void EnsureNotDisposed()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(SvcProvider));
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

        foreach (var instance in _singletonInstances.Values)
        {
            if (instance is IAsyncDisposable asyncDisposable)
                await asyncDisposable.DisposeAsync().ConfigureAwait(false);
            (instance as IDisposable)?.Dispose();
        }
        _disposed = true;
    }

    private void DisposeInstances(Action<object> disposeAction)
    {
        foreach (var instance in _singletonInstances.Values)
            disposeAction(instance);
    }
    #endregion
}
