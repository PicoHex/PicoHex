namespace PicoHex.DependencyInjection;

public class SvcProvider(ISvcRegistry registry, ISvcScopeFactory scopeFactory) : ISvcProvider
{
    public object? Resolve(Type serviceType)
    {
        var factory = registry.GetOrAddInstanceFactory(
            serviceType,
            svcProvider =>
            {
                var resolutionStack = new Stack<Type>();
                return Resolve(serviceType, svcProvider, resolutionStack);
            }
        );

        return factory(this);
    }

    public ISvcScope CreateScope() => scopeFactory.CreateScope(this);

    private object? Resolve(Type serviceType, ISvcProvider svcProvider, Stack<Type> resolutionStack)
    {
        var descriptor = registry.GetServiceDescriptor(serviceType);
        return descriptor.Lifetime switch
        {
            SvcLifetime.Singleton
                => registry.GetSingletonInstance(
                    serviceType,
                    () => CreateInstance(descriptor, svcProvider, resolutionStack)
                ),
            SvcLifetime.PerThread
                => registry.GetPerThreadInstance(
                    serviceType,
                    () => CreateInstance(descriptor, svcProvider, resolutionStack)
                ),
            _ => CreateInstance(descriptor, svcProvider, resolutionStack)
        };
    }

    private object? CreateInstance(
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

        // Check for circular dependencies
        if (resolutionStack.Contains(implementationType))
            throw new InvalidOperationException(
                $"Circular dependency detected for type {implementationType.Name}."
            );

        var constructor =
            descriptor.Constructor
            ?? throw new InvalidOperationException(
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
