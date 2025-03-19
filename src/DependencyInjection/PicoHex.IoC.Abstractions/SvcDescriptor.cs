namespace PicoHex.IoC.Abstractions;

public class SvcDescriptor(Type serviceType, Type implementationType, SvcLifetime lifetime)
{
    public Type ServiceType { get; } = serviceType;
    public Type? ImplementationType { get; } = implementationType;
    public SvcLifetime Lifetime { get; } = lifetime;
    public Func<ISvcProvider, object>? Factory { get; }

    public SvcDescriptor(Type serviceType, Func<ISvcProvider, object> factory, SvcLifetime lifetime)
        : this(serviceType, serviceType, lifetime) => Factory = factory;

    public SvcDescriptor(Type serviceType, SvcLifetime lifetime)
        : this(serviceType, serviceType, lifetime) { }
}
