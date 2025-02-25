namespace PicoHex.IoC;

/// <summary>
/// Service lifetime options
/// </summary>
public enum ServiceLifetime
{
    /// <summary>
    /// A new instance is created each time the service is requested
    /// </summary>
    Transient,

    /// <summary>
    /// A single instance is created and shared across all requests
    /// </summary>
    Singleton,

    /// <summary>
    /// A single instance is created for each scope
    /// </summary>
    Scoped
}

/// <summary>
/// Collection of service descriptors
/// </summary>
public class ServiceCollection
{
    private readonly List<ServiceDescriptor> _descriptors = new();

    /// <summary>
    /// Adds a service with the specified implementation
    /// </summary>
    public void Add(ServiceDescriptor descriptor)
    {
        _descriptors.Add(descriptor);
    }

    /// <summary>
    /// Gets all registered service descriptors
    /// </summary>
    public IReadOnlyList<ServiceDescriptor> GetDescriptors() => _descriptors.AsReadOnly();

    /// <summary>
    /// Adds a transient service
    /// </summary>
    public void AddTransient<TService, TImplementation>()
        where TService : class
        where TImplementation : class, TService
    {
        Add(
            new ServiceDescriptor(
                typeof(TService),
                typeof(TImplementation),
                ServiceLifetime.Transient
            )
        );
    }

    /// <summary>
    /// Adds a singleton service
    /// </summary>
    public void AddSingleton<TService, TImplementation>()
        where TService : class
        where TImplementation : class, TService
    {
        Add(
            new ServiceDescriptor(
                typeof(TService),
                typeof(TImplementation),
                ServiceLifetime.Singleton
            )
        );
    }

    /// <summary>
    /// Adds a scoped service
    /// </summary>
    public void AddScoped<TService, TImplementation>()
        where TService : class
        where TImplementation : class, TService
    {
        Add(
            new ServiceDescriptor(typeof(TService), typeof(TImplementation), ServiceLifetime.Scoped)
        );
    }
}
