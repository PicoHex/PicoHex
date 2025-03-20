namespace PicoHex.IoC;

public sealed class SvcProvider(ISvcContainer container, ISvcScopeFactory scopeFactory)
    : ISvcProvider
{
    private volatile bool _disposed;
    private readonly ConcurrentStack<Type> _resolving = new();
    private static readonly ConcurrentDictionary<Type, object> Singletons = new();
    private readonly ThreadLocal<Dictionary<Type, object>> _perThread = new(() => new());

    public object Resolve(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
            Type serviceType
    )
    {
        var svcDescriptor = container.GetDescriptor(serviceType);
        if (svcDescriptor is null)
            throw new InvalidOperationException($"Type {serviceType.Name} is not registered.");

        // 循环依赖检测
        if (_resolving.Contains(serviceType))
        {
            var cycle = string.Join(" → ", _resolving.Reverse().Select(t => t.Name));
            throw new InvalidOperationException(
                $"Circular dependency detected: {cycle} → {serviceType.Name}"
            );
        }

        _resolving.Push(serviceType);
        try
        {
            return svcDescriptor.Lifetime switch
            {
                SvcLifetime.Transient => CreateTransient(svcDescriptor),
                SvcLifetime.Singleton => GetSingleton(svcDescriptor),
                SvcLifetime.Scoped => svcDescriptor.Factory!(this),
                SvcLifetime.PerThread => CreatePerThread(svcDescriptor),
                _ => throw new ArgumentOutOfRangeException()
            };
        }
        finally
        {
            _resolving.TryPop(out _);
        }
    }

    private object CreateTransient(SvcDescriptor svcDescriptor) => svcDescriptor.Factory!(this);

    private object GetSingleton(SvcDescriptor svcDescriptor) =>
        Singletons.GetOrAdd(svcDescriptor.ServiceType, svcDescriptor.Factory!(this));

    private object CreatePerThread(SvcDescriptor svcDescriptor)
    {
        var instances = _perThread.Value!;
        if (instances.TryGetValue(svcDescriptor.ServiceType, out var instance))
            return instance;
        instance = svcDescriptor.Factory!(this);
        instances[svcDescriptor.ServiceType] = instance;
        return instance;
    }

    public ISvcScope CreateScope() => scopeFactory.CreateScope(this);

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

    private void Dispose(bool disposing)
    {
        if (_disposed)
            return;
        if (disposing)
        {
            foreach (var service in _perThread.Value!)
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
        foreach (var service in _perThread.Value!)
        {
            if (service.Value is IDisposable disposable)
                disposable.Dispose();
            if (service.Value is IAsyncDisposable asyncDisposable)
                await asyncDisposable.DisposeAsync().ConfigureAwait(false);
        }
        _disposed = true;
    }
}
