namespace PicoHex.IoC;

/// <summary>
/// Describes a service with its implementation and lifetime
/// </summary>
public class ServiceDescriptor
{
    /// <summary>
    /// Creates a new service descriptor
    /// </summary>
    public ServiceDescriptor(Type serviceType, Type implementationType, ServiceLifetime lifetime)
    {
        ServiceType = serviceType;
        ImplementationType = implementationType;
        Lifetime = lifetime;
    }

    /// <summary>
    /// The service type
    /// </summary>
    public Type ServiceType { get; }

    /// <summary>
    /// The implementation type
    /// </summary>
    public Type ImplementationType { get; }

    /// <summary>
    /// The service lifetime
    /// </summary>
    public ServiceLifetime Lifetime { get; }
}
