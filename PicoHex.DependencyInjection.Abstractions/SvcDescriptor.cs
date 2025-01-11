namespace PicoHex.DependencyInjection.Abstractions;

public sealed class SvcDescriptor
{
    public SvcDescriptor(Type serviceType, Type implementationType, SvcLifetime lifetime)
        : this(serviceType, lifetime)
    {
        ImplementationType = implementationType;
    }

    public SvcDescriptor(Type serviceType, Func<ISvcProvider, object> factory, SvcLifetime lifetime)
        : this(serviceType, lifetime)
    {
        Factory = factory;
    }

    private SvcDescriptor(Type serviceType, SvcLifetime lifetime)
    {
        ServiceType = serviceType;
        Lifetime = lifetime;
    }

    public Type ServiceType { get; }
    public Type? ImplementationType { get; }
    public Func<ISvcProvider, object>? Factory { get; }
    public SvcLifetime Lifetime { get; }
}
