namespace Pico.IoC.Abs;

public sealed class SvcDescriptor
{
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
    public Type ServiceType { get; }

    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
    public Type? ImplementationType { get; }
    public Func<ISvcProvider, object>? Factory { get; set; }
    public object? SingleInstance { get; set; }
    public SvcLifetime Lifetime { get; }

    public SvcDescriptor(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
            Type serviceType,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
            Type implementationType,
        SvcLifetime lifetime
    )
        : this(serviceType, lifetime)
    {
        ImplementationType = implementationType;
    }

    public SvcDescriptor(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
            Type serviceType,
        object singleInstance
    )
        : this(serviceType, SvcLifetime.Singleton)
    {
        ImplementationType = singleInstance.GetType();
        SingleInstance = singleInstance;
    }

    public SvcDescriptor(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
            Type serviceType,
        Func<ISvcProvider, object> factory,
        SvcLifetime lifetime
    )
        : this(serviceType, lifetime)
    {
        Factory = factory;
    }

    internal SvcDescriptor(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
            Type serviceType,
        SvcLifetime lifetime
    )
    {
        ServiceType = serviceType;
        ImplementationType = serviceType;
        Lifetime = lifetime;
    }
}
