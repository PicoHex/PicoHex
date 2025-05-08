namespace Pico.DI;

public sealed class SvcContainer(ISvcProviderFactory providerFactory) : ISvcContainer
{
    private readonly ConcurrentDictionary<Type, List<SvcDescriptor>> _descriptors = new();
    private Lazy<ISvcProvider>? _lazyProvider;
    private readonly Lock _lock = new();

    public ISvcContainer Register(SvcDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);

        _descriptors.TryAdd(descriptor.ServiceType, []);
        _descriptors[descriptor.ServiceType].Add(descriptor);

        return this;
    }

    public ISvcProvider GetProvider()
    {
        if (_lazyProvider is not null)
            return _lazyProvider.Value;
        lock (_lock)
        {
            _lazyProvider ??= new Lazy<ISvcProvider>(
                () => providerFactory.CreateProvider(this),
                LazyThreadSafetyMode.ExecutionAndPublication
            );
        }
        return _lazyProvider.Value;
    }

    public List<SvcDescriptor> GetDescriptors(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] Type type
    )
    {
        var descriptors = _descriptors.GetValueOrDefault(type);

        if (descriptors is not null)
            return descriptors;

        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IEnumerable<>))
        {
            var elementType = type.GetGenericArguments()[0];
            descriptors = _descriptors.GetValueOrDefault(elementType);
            if (descriptors is not null)
                return descriptors;
        }

        if (type.IsConstructedGenericType)
        {
            var closedGenericDescriptor = CreateClosedGenericDescriptor(type);
            Register(closedGenericDescriptor);
            descriptors = _descriptors.GetValueOrDefault(type);
        }

        return descriptors ?? throw new ServiceNotRegisteredException(type);
    }

    public SvcDescriptor GetDescriptor(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] Type type
    ) => GetDescriptors(type).Last();

    #region private methods

    private SvcDescriptor CreateClosedGenericDescriptor(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
            Type serviceType
    )
    {
        // Check existing registration first
        if (_descriptors.GetValueOrDefault(serviceType)?.Last() is { } existing)
            return existing;

        // Resolve from open generic
        var openGenericType = serviceType.GetGenericTypeDefinition();
        var openDescriptor =
            _descriptors.GetValueOrDefault(openGenericType)?.Last()
            ?? throw new ServiceNotRegisteredException(openGenericType);

        // Create closed generic type
        var closedType = openDescriptor.ImplementationType!.MakeGenericType(
            serviceType.GenericTypeArguments
        );

        // Auto-register closed generic
        var closedDescriptor = new SvcDescriptor(serviceType, closedType, openDescriptor.Lifetime);
        return closedDescriptor;
    }

    #endregion
}
