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

        if (IsClosedGenericRequest(serviceType))
            return ResolveInstance(CreateClosedGenericDescriptor(serviceType));

        return ResolveInstance(
            container.GetDescriptor(serviceType)
                ?? throw new ServiceNotRegisteredException(serviceType)
        );
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

    private bool IsClosedGenericRequest(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
            Type serviceType
    ) => serviceType.IsConstructedGenericType;

    private SvcDescriptor CreateClosedGenericDescriptor(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
            Type serviceType
    )
    {
        // Check existing registration first
        if (container.GetDescriptor(serviceType) is { } existing)
            return existing;

        // Resolve from open generic
        var openGenericType = serviceType.GetGenericTypeDefinition();
        var openDescriptor =
            container.GetDescriptor(openGenericType)
            ?? throw new ServiceNotRegisteredException(openGenericType);

        // Create closed generic type
        var closedType = openDescriptor.ImplementationType!.MakeGenericType(
            serviceType.GenericTypeArguments
        );

        // Auto-register closed generic
        var closedDescriptor = new SvcDescriptor(serviceType, closedType, openDescriptor.Lifetime);

        container.Register(closedDescriptor);
        return closedDescriptor;
    }

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
            ? Array.CreateInstance(elementType, 0)
            : CreateServiceArray(elementType, descriptors);
    }

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

    private object ResolveInstance(SvcDescriptor descriptor)
    {
        if (descriptor.Factory is null && descriptor.SingleInstance is null)
            lock (descriptor)
                descriptor.Factory ??= SvcFactory.CreateAotFactory(descriptor);

        return descriptor.Lifetime switch
        {
            SvcLifetime.Transient => descriptor.Factory!(this),
            SvcLifetime.Singleton
                => _singletonInstances.GetOrAdd(
                    descriptor.ServiceType,
                    new Lazy<object>(
                        () => descriptor.SingleInstance ??= descriptor.Factory!(this)
                    ).Value
                ),
            SvcLifetime.Scoped => descriptor.Factory!(this),
            _
                => throw new ArgumentOutOfRangeException(
                    $"Unsupported service lifetime: {descriptor.Lifetime}"
                )
        };
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
