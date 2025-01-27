namespace PicoHex.DependencyInjection;

public class SvcRegistry(ISvcProviderFactory providerFactory) : ISvcRegistry
{
    private readonly ConcurrentDictionary<Type, SvcDescriptor> _descriptors = new();
    private readonly ConcurrentDictionary<Type, object?> _singletonInstances = new();
    private readonly ConcurrentDictionary<Type, ThreadLocal<object?>> _perThreadInstances = new();
    private readonly ConcurrentDictionary<Type, Func<ISvcProvider, object?>> _instanceFactories =
        new();

    public ISvcRegistry AddServiceDescriptor(SvcDescriptor descriptor)
    {
        // 新增校验：确保开放泛型注册的合法性
        if (
            descriptor.ServiceType.IsGenericTypeDefinition
            && descriptor.ImplementationType is { IsGenericTypeDefinition: false }
        )
            throw new ArgumentException("开放泛型服务必须对应开放泛型实现类型");

        if (!_descriptors.TryAdd(descriptor.ServiceType, descriptor))
            throw new InvalidOperationException(
                $"Service descriptor for type {descriptor.ServiceType} already exists."
            );

        if (descriptor.Factory is not null)
            GetOrAddInstanceFactory(descriptor.ServiceType, descriptor.Factory);

        return this;
    }

    public Func<ISvcProvider, object?> GetOrAddInstanceFactory(
        Type serviceType,
        Func<ISvcProvider, object?> factory
    ) => _instanceFactories.GetOrAdd(serviceType, factory);

    public object? GetSingletonInstance(Type type, Func<object?> instanceFactory)
    {
        if (_singletonInstances.TryGetValue(type, out var instance))
            return instance;

        instance = instanceFactory();
        _singletonInstances[type] = instance;
        return instance;
    }

    public object? GetPerThreadInstance(Type type, Func<object?> instanceFactory)
    {
        if (_perThreadInstances.TryGetValue(type, out var instance))
            return instance.Value;

        instance = new ThreadLocal<object?>(instanceFactory);
        _perThreadInstances[type] = instance;
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
