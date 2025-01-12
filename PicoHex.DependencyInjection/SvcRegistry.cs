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
            descriptor.Factory ?? Resolve(descriptor.ServiceType, svcProvider, new Stack<Type>());
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

    private object? Resolve(Type type, ISvcProvider svcProvider, Stack<Type> resolutionStack)
    {
        var descriptor = GetServiceDescriptor(type);
        return descriptor.Lifetime switch
        {
            SvcLifetime.Singleton
                => GetSingletonInstance(
                    type,
                    () => CreateInstance(descriptor, svcProvider, resolutionStack)
                ),
            SvcLifetime.PerThread
                => GetPerThreadInstance(
                    type,
                    () => CreateInstance(descriptor, svcProvider, resolutionStack)
                ),
            _ => CreateInstance(descriptor, svcProvider, resolutionStack)
        };
    }

    private object CreateInstance(
        SvcDescriptor descriptor,
        ISvcProvider svcProvider,
        Stack<Type> resolutionStack
    )
    {
        if (descriptor.Factory is not null)
            return descriptor.Factory(svcProvider);

        if (descriptor.ImplementationType is null)
            throw new InvalidOperationException("No factory or implementation type found.");

        var implementationType = descriptor.ImplementationType;

        if (resolutionStack.Contains(implementationType))
            throw new InvalidOperationException(
                $"Circular dependency detected for type {implementationType.Name}."
            );

        var constructor = descriptor.Constructor;
        if (constructor is null)
            throw new InvalidOperationException(
                $"No public constructor found for type {implementationType.Name}"
            );
        resolutionStack.Push(implementationType);

        try
        {
            var parameters = constructor
                .Parameters
                .Select(param => Resolve(param.ParameterType, svcProvider, resolutionStack))
                .ToArray();
            return constructor.ConstructorInfo.Invoke(parameters);
        }
        finally
        {
            resolutionStack.Pop();
        }
    }
}
