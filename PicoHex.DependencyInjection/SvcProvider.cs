namespace PicoHex.DependencyInjection;

public class SvcProvider(ISvcRegistry registry, ISvcScopeFactory scopeFactory) : ISvcProvider
{
    public object? Resolve(Type implementationType) =>
        Resolve(implementationType, new Stack<Type>());

    private object? Resolve(Type implementationType, Stack<Type> resolutionStack)
    {
        var descriptor = registry.GetServiceDescriptor(implementationType);
        return descriptor.Lifetime switch
        {
            SvcLifetime.Singleton
                => registry.GetSingletonInstance(
                    implementationType,
                    () => CreateInstance(descriptor, resolutionStack)
                ),
            SvcLifetime.PerThread
                => registry.GetPerThreadInstance(
                    implementationType,
                    () => CreateInstance(descriptor, resolutionStack)
                ),
            _ => CreateInstance(descriptor, resolutionStack)
        };
    }

    private object CreateInstance(SvcDescriptor descriptor, Stack<Type> resolutionStack)
    {
        if (descriptor.Factory is not null)
            return descriptor.Factory(this);

        if (descriptor.ImplementationType is null)
            throw new InvalidOperationException("No factory or implementation type found.");

        var implementationType = descriptor.ImplementationType;

        if (resolutionStack.Contains(implementationType))
            throw new InvalidOperationException(
                $"Circular dependency detected for type {implementationType.Name}."
            );

        var constructor = descriptor.Constructor;
        if (constructor is null)
        {
            throw new InvalidOperationException(
                $"No public constructor found for type {implementationType.Name}"
            );
        }
        resolutionStack.Push(implementationType);

        try
        {
            var parameters = constructor
                .Parameters
                .Select(param => Resolve(param.ParameterType, resolutionStack))
                .ToArray();
            return constructor.ConstructorInfo.Invoke(parameters);
        }
        finally
        {
            resolutionStack.Pop();
        }
    }

    public ISvcScope CreateScope() => scopeFactory.CreateScope(this);
}
