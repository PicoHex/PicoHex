namespace PicoHex.DependencyInjection;

public class SvcRegistry(ISvcProviderFactory providerFactory) : ISvcRegistry
{
    private readonly ConcurrentDictionary<Type, SvcDescriptor> _descriptors = new();
    private readonly ConcurrentDictionary<Type, object?> _singletonInstance = new();
    private readonly ConcurrentDictionary<Type, ThreadLocal<object?>> _perThreadInstance = new();
    private readonly ConcurrentDictionary<Type, Func<ISvcProvider, object?>> _instanceFactory =
        new();

    public ISvcRegistry AddServiceDescriptor(SvcDescriptor descriptor)
    {
        if (!_descriptors.TryAdd(descriptor.ServiceType, descriptor))
            throw new InvalidOperationException(
                $"Service descriptor for type {descriptor.ServiceType} already exists."
            );
        _instanceFactory[descriptor.ServiceType] = svcProvider =>
            descriptor.Factory ?? svcProvider.Resolve(descriptor.ServiceType);
        return this;
    }

    public Func<ISvcProvider, object?> GetInstanceFactory(Type implementationType)
    {
        if (!_instanceFactory.TryGetValue(implementationType, out var factory))
            throw new InvalidOperationException(
                $"No instance factory found for type {implementationType}."
            );
        return factory;
    }

    public object? GetSingletonInstance(Type type, Func<object?> instanceFactory)
    {
        if (_singletonInstance.TryGetValue(type, out var instance))
            return instance;
        instance = instanceFactory();
        _singletonInstance[type] = instance;
        return instance;
    }

    public object? GetPerThreadInstance(Type type, Func<object?> instanceFactory)
    {
        if (_perThreadInstance.TryGetValue(type, out var instance))
            return instance.Value;
        instance = new ThreadLocal<object?>(instanceFactory);
        _perThreadInstance[type] = instance;
        return instance.Value;
    }

    public ISvcProvider CreateProvider() => providerFactory.CreateServiceProvider(this);

    public SvcDescriptor GetServiceDescriptor(Type type)
    {
        if (!_descriptors.TryGetValue(type, out var descriptor))
            throw new InvalidOperationException($"No service descriptor found for type {type}.");
        return descriptor;
    }
}
