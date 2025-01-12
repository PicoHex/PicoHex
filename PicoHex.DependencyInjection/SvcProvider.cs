namespace PicoHex.DependencyInjection;

public class SvcProvider(ISvcRegistry registry, ISvcScopeFactory scopeFactory) : ISvcProvider
{
    private readonly ConcurrentStack<Type> _resolutionStack = new();

    public object? Resolve(Type implementationType)
    {
        var descriptor = registry.GetServiceDescriptor(implementationType);
        return descriptor.Lifetime switch
        {
            SvcLifetime.Singleton
                => registry.GetSingletonInstance(
                    implementationType,
                    () => CreateInstance(descriptor)
                ),
            SvcLifetime.PerThread
                => registry.GetPerThreadInstance(
                    implementationType,
                    () => CreateInstance(descriptor)
                ),
            _ => CreateInstance(descriptor)
        };
    }

    public ISvcScope CreateScope() => scopeFactory.CreateScope(this);

    private object CreateInstance(SvcDescriptor descriptor)
    {
        if (descriptor.Factory is not null)
            return descriptor.Factory(this);

        if (descriptor.ImplementationType is null)
            throw new InvalidOperationException("No factory or implementation type found.");

        var implementationType = descriptor.ImplementationType;

        if (_resolutionStack.Contains(implementationType))
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
        _resolutionStack.Push(implementationType);

        try
        {
            var parameters = constructor
                .Parameters
                .Select(param => Resolve(param.ParameterType))
                .ToArray();
            return constructor.ConstructorInfo.Invoke(parameters);
        }
        finally
        {
            _resolutionStack.TryPop(out _);
        }
    }
}
