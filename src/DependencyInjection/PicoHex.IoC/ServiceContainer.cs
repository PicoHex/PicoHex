namespace PicoHex.IoC;

/// <summary>
/// AOT-compatible service container
/// </summary>
public class ServiceContainer : IServiceProvider, IDisposable
{
    private readonly Dictionary<Type, ServiceDescriptor> _descriptors = new();
    private readonly Dictionary<Type, object> _singletons = new();
    private readonly Dictionary<Type, Func<ServiceContainer, object>> _factories = new();

    public ServiceContainer(ServiceCollection services)
    {
        // Register the container itself
        _singletons[typeof(IServiceProvider)] = this;
        _singletons[typeof(ServiceContainer)] = this;

        foreach (var descriptor in services.GetDescriptors())
        {
            _descriptors[descriptor.ServiceType] = descriptor;
        }

        // Find and use the generated factory provider
        var factoryProviderType = Type.GetType("PicoHex.IoC.GeneratedFactoryProvider, PicoHex.IoC");
        if (factoryProviderType != null)
        {
            var provider = Activator.CreateInstance(factoryProviderType) as IFactoryProvider;
            provider?.RegisterFactories(this, _factories);
        }
    }

    /// <summary>
    /// Gets a service of the specified type
    /// </summary>
    public T GetService<T>()
        where T : class
    {
        return (T)GetService(typeof(T));
    }

    /// <summary>
    /// Gets a service of the specified type
    /// </summary>
    public object GetService(Type serviceType)
    {
        if (_singletons.TryGetValue(serviceType, out var singleton))
        {
            return singleton;
        }

        if (!_descriptors.TryGetValue(serviceType, out var descriptor))
        {
            throw new InvalidOperationException($"Service {serviceType.Name} is not registered");
        }

        if (!_factories.TryGetValue(descriptor.ImplementationType, out var factory))
        {
            throw new InvalidOperationException(
                $"No factory found for {descriptor.ImplementationType.Name}. "
                    + "Make sure the source generator has processed this type."
            );
        }

        var instance = factory(this);

        if (descriptor.Lifetime == ServiceLifetime.Singleton)
        {
            _singletons[serviceType] = instance;
        }

        return instance;
    }

    /// <summary>
    /// Disposes the container and all disposable singletons
    /// </summary>
    public void Dispose()
    {
        foreach (var singleton in _singletons.Values)
        {
            if (singleton is IDisposable disposable)
            {
                disposable.Dispose();
            }
        }

        _singletons.Clear();
        GC.SuppressFinalize(this);
    }
}
