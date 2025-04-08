namespace PicoHex.DI;

public sealed class SvcProvider(ISvcContainer container, ISvcScopeFactory scopeFactory)
    : ISvcProvider
{
    private readonly ConcurrentDictionary<Type, object> _singletons = new();
    private volatile bool _disposed;

    public object Resolve(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
            Type serviceType
    )
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(SvcProvider));

        if (
            serviceType.IsGenericType
            && serviceType.GetGenericTypeDefinition() == typeof(IEnumerable<>)
        )
        {
            var elementType = serviceType.GetGenericArguments()[0];

            var descriptors =
                container.GetDescriptors(elementType)
                ?? throw new InvalidOperationException(
                    $"Service of type {elementType} not registered."
                );
            return ResolveAll(elementType, descriptors);
        }

        var svcDescriptor =
            container.GetDescriptor(serviceType)
            ?? throw new InvalidOperationException(
                $"Service of type {serviceType} not registered."
            );
        return ResolveLast(svcDescriptor);
    }

    private object ResolveLast(SvcDescriptor svcDescriptor) => GetOrCreateInstance(svcDescriptor);

    private object ResolveAll(Type elementType, IList<SvcDescriptor> svcDescriptors)
    {
        if (svcDescriptors.Count is 0)
        {
            var emptyArray = Array.CreateInstance(elementType, 0);
            return emptyArray;
        }

        var instances = svcDescriptors.Select(GetOrCreateInstance).ToArray();

        var array = Array.CreateInstance(elementType, instances.Length);
        Array.Copy(instances, array, instances.Length);
        return array;
    }

    private object GetOrCreateInstance(SvcDescriptor svcDescriptor) =>
        svcDescriptor.Lifetime switch
        {
            SvcLifetime.Transient => GetTransientInstance(svcDescriptor),
            SvcLifetime.Singleton => GetSingletonInstance(svcDescriptor),
            SvcLifetime.Scoped => GetScopedInstance(svcDescriptor),
            _ => throw new ArgumentOutOfRangeException()
        };

    private object GetTransientInstance(SvcDescriptor svcDescriptor)
    {
        if (svcDescriptor.Factory is not null)
            return svcDescriptor.Factory(this);
        lock (svcDescriptor)
            svcDescriptor.Factory ??= SvcFactory.CreateAotFactory(svcDescriptor);
        return svcDescriptor.Factory(this);
    }

    private object GetSingletonInstance(SvcDescriptor svcDescriptor) =>
        _singletons.GetOrAdd(
            svcDescriptor.ServiceType,
            new Lazy<object>(() =>
            {
                if (svcDescriptor.SingleInstance is not null)
                    return svcDescriptor.SingleInstance;
                svcDescriptor.Factory ??= SvcFactory.CreateAotFactory(svcDescriptor);
                svcDescriptor.SingleInstance = svcDescriptor.Factory(this);
                return svcDescriptor.SingleInstance;
            }).Value
        );

    private object GetScopedInstance(SvcDescriptor svcDescriptor)
    {
        if (svcDescriptor.Factory is not null)
            return svcDescriptor.Factory(this);
        lock (svcDescriptor)
            svcDescriptor.Factory ??= SvcFactory.CreateAotFactory(svcDescriptor);
        return svcDescriptor.Factory(this);
    }

    public ISvcScope CreateScope() => scopeFactory.CreateScope(container, this);

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
            foreach (var service in _singletons)
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
        foreach (var service in _singletons)
        {
            if (service.Value is IDisposable disposable)
                disposable.Dispose();
            if (service.Value is IAsyncDisposable asyncDisposable)
                await asyncDisposable.DisposeAsync().ConfigureAwait(false);
        }
        _disposed = true;
    }
}
