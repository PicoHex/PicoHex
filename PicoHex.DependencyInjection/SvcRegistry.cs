namespace PicoHex.DependencyInjection;

public class SvcRegistry(ISvcProviderFactory providerFactory) : ISvcRegistry
{
    private readonly Dictionary<Type, SvcDescriptor> _descriptors = new();
    private static readonly ConcurrentDictionary<Type, object?> SingletonInstance = new();
    private static readonly ConcurrentDictionary<Type, ThreadLocal<object?>> PerThreadInstance =
        new();

    public ISvcRegistry AddServiceDescriptor(SvcDescriptor descriptor)
    {
        _descriptors.Add(descriptor.ServiceType, descriptor);
        return this;
    }

    public object? GetSingletonInstance(Type type, Func<object?> instanceFactory)
    {
        if (SingletonInstance.TryGetValue(type, out var instance))
            return instance;
        instance = instanceFactory();
        SingletonInstance[type] = instance;
        return instance;
    }

    public object? GetPerThreadInstance(Type type, Func<object?> instanceFactory)
    {
        if (PerThreadInstance.TryGetValue(type, out var instance))
            return instance.Value;
        instance = new ThreadLocal<object?>(instanceFactory);
        PerThreadInstance[type] = instance;
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
