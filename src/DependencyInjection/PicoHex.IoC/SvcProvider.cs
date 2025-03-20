namespace PicoHex.IoC;

public sealed class SvcProvider(ISvcContainer container, ISvcScopeFactory scopeFactory)
    : ISvcProvider
{
    private volatile bool _disposed;
    private readonly ConcurrentStack<Type> _resolving = new();
    private static readonly ConcurrentDictionary<Type, object> Singletons = new();
    private static readonly ConcurrentDictionary<Type, ThreadLocal<object?>> PerThread = new();

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
                SvcLifetime.Transient => svcDescriptor.Factory!(this),
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

    private object GetSingleton(SvcDescriptor svcDescriptor) =>
        svcDescriptor.Instance
        ?? Singletons.GetOrAdd(svcDescriptor.ServiceType, svcDescriptor.Factory!(this));

    private object CreatePerThread(SvcDescriptor svcDescriptor)
    {
        if (PerThread.TryGetValue(svcDescriptor.ServiceType, out var instance))
            return instance.Value!;

        instance = new ThreadLocal<object?>(() => svcDescriptor.Factory!(this));
        PerThread[svcDescriptor.ServiceType] = instance;
        return instance.Value!;
    }

    public ISvcScope CreateScope() => scopeFactory.CreateScope(this);

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
            foreach (var service in PerThread)
            {
                if (service.Value.Value is IDisposable disposable)
                    disposable.Dispose();
            }
        }
        _disposed = true;
    }

    private async ValueTask DisposeAsyncCore()
    {
        if (_disposed)
            return;
        foreach (var service in PerThread)
        {
            if (service.Value.Value is IDisposable disposable)
                disposable.Dispose();
            if (service.Value.Value is IAsyncDisposable asyncDisposable)
                await asyncDisposable.DisposeAsync().ConfigureAwait(false);
        }
        _disposed = true;
    }
}
