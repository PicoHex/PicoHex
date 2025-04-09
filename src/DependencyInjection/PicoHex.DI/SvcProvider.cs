namespace PicoHex.DI;

public sealed class SvcProvider(ISvcContainer container, ISvcScopeFactory scopeFactory)
    : ISvcProvider
{
    private readonly ConcurrentDictionary<Type, object> _singletonInstances = new();
    private volatile bool _disposed;

    public object Resolve(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
            Type serviceType
    )
    {
        EnsureNotDisposed();

        if (IsEnumerableRequest(serviceType, out var elementType))
            return ResolveAllServices(elementType);

        return TryResolveClosedGenericDescriptor(serviceType, out var descriptor)
            ? ResolveInstance(descriptor)
            : ResolveRegisteredService(serviceType);
    }

    public ISvcScope CreateScope()
    {
        EnsureNotDisposed();
        return scopeFactory.CreateScope(container, this);
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

    private static bool IsEnumerableRequest(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
            Type serviceType,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
            out Type elementType
    )
    {
        elementType = null!;
        if (
            !serviceType.IsGenericType
            || serviceType.GetGenericTypeDefinition() != typeof(IEnumerable<>)
        )
            return false;

        elementType = serviceType.GetGenericArguments()[0];
        return true;
    }

    private object ResolveAllServices(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
            Type elementType
    )
    {
        var descriptors =
            container.GetDescriptors(elementType)
            ?? throw new ServiceNotRegisteredException(elementType);

        return descriptors.Count is 0
            ? CreateEmptyArray(elementType)
            : CreateServiceArray(elementType, descriptors);
    }

    private static Array CreateEmptyArray(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
            Type elementType
    ) => Array.CreateInstance(elementType, 0);

    private Array CreateServiceArray(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
            Type elementType,
        IList<SvcDescriptor> descriptors
    )
    {
        var instances = descriptors.Select(ResolveInstance).ToArray();
        var array = Array.CreateInstance(elementType, instances.Length);
        Array.Copy(instances, array, instances.Length);
        return array;
    }

    private bool TryResolveClosedGenericDescriptor(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
            Type serviceType,
        out SvcDescriptor descriptor
    )
    {
        descriptor = null!;
        if (!serviceType.IsConstructedGenericType)
            return false;

        // Check existing registration
        if ((descriptor = container.GetDescriptor(serviceType)!) is not null)
            return true;

        // Try to create from open generic
        var openGenericType = serviceType.GetGenericTypeDefinition();
        var openDescriptor = container.GetDescriptor(openGenericType);
        if (openDescriptor is null)
            return false;

        var closedType = openDescriptor
            .ImplementationType
            .MakeGenericType(serviceType.GenericTypeArguments);
        descriptor = new SvcDescriptor(serviceType, closedType, openDescriptor.Lifetime);
        container.Register(descriptor);
        return true;
    }

    private object ResolveRegisteredService(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
            Type serviceType
    )
    {
        var descriptor =
            container.GetDescriptor(serviceType)
            ?? throw new ServiceNotRegisteredException(serviceType);
        return ResolveInstance(descriptor);
    }

    private object ResolveInstance(SvcDescriptor descriptor) =>
        descriptor.Lifetime switch
        {
            SvcLifetime.Transient => CreateTransientInstance(descriptor),
            SvcLifetime.Singleton => GetOrCreateSingleton(descriptor),
            SvcLifetime.Scoped => CreateScopedInstance(descriptor),
            _ => throw new UnsupportedLifetimeException(descriptor.Lifetime)
        };

    private object CreateTransientInstance(SvcDescriptor descriptor)
    {
        EnsureFactoryInitialized(descriptor);
        return descriptor.Factory!(this);
    }

    private object GetOrCreateSingleton(SvcDescriptor descriptor) =>
        _singletonInstances.GetOrAdd(
            descriptor.ServiceType,
            new Lazy<object>(() => CreateSingleton(descriptor)).Value
        );

    private object CreateSingleton(SvcDescriptor descriptor)
    {
        EnsureFactoryInitialized(descriptor);
        return descriptor.SingleInstance ??= descriptor.Factory!(this);
    }

    private object CreateScopedInstance(SvcDescriptor descriptor)
    {
        EnsureFactoryInitialized(descriptor);
        return descriptor.Factory!(this);
    }

    private static void EnsureFactoryInitialized(SvcDescriptor descriptor)
    {
        if (descriptor.Factory is not null)
            return;

        lock (descriptor)
            descriptor.Factory ??= SvcFactory.CreateAotFactory(descriptor);
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
