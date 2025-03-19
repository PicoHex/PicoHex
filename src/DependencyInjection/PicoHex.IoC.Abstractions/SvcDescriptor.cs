namespace PicoHex.IoC.Abstractions;

public class SvcDescriptor(
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
        Type serviceType,
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
        Type implementationType,
    SvcLifetime lifetime
)
{
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
    public Type ServiceType { get; } = serviceType;

    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
    public Type ImplementationType { get; } = implementationType;
    public SvcLifetime Lifetime { get; } = lifetime;
    public Func<ISvcProvider, object>? Factory { get; set; }

    public SvcDescriptor(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
            Type serviceType,
        Func<ISvcProvider, object> factory,
        SvcLifetime lifetime
    )
        : this(serviceType, serviceType, lifetime) => Factory = factory;

    public SvcDescriptor(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
            Type serviceType,
        SvcLifetime lifetime
    )
        : this(serviceType, serviceType, lifetime) { }
}
