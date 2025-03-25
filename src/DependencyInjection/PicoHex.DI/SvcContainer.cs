namespace PicoHex.DI;

public sealed class SvcContainer(ISvcProviderFactory providerFactory) : ISvcContainer
{
    private readonly ConcurrentDictionary<Type, SvcDescriptor> _descriptors = new();

    public ISvcContainer Register(SvcDescriptor descriptor)
    {
        _descriptors.TryAdd(descriptor.ServiceType, descriptor);
        _descriptors.TryAdd(descriptor.ImplementationType, descriptor);
        return this;
    }

    public ISvcProvider CreateProvider() => providerFactory.CreateProvider(this);

    public SvcDescriptor? GetDescriptor(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] Type type
    ) => _descriptors.GetValueOrDefault(type);
}
