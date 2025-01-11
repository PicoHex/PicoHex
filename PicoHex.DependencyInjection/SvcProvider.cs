namespace PicoHex.DependencyInjection;

public class SvcProvider(ISvcRegistry registry, ISvcScopeFactory scopeFactory) : ISvcProvider
{
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
        var constructor = implementationType.GetConstructors().FirstOrDefault();
        if (constructor == null)
        {
            throw new InvalidOperationException(
                $"No public constructor found for type {implementationType.Name}"
            );
        }

        var parameters = constructor
            .GetParameters()
            .Select(param => Resolve(param.ParameterType))
            .ToArray();

        return constructor.Invoke(parameters);
    }
}
