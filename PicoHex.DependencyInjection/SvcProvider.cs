namespace PicoHex.DependencyInjection;

public class SvcProvider(ISvcRegistry registry, ISvcScopeFactory scopeFactory) : ISvcProvider
{
    public object? Resolve(Type implementationType)
    {
        var descriptor = registry.GetServiceDescriptor(implementationType);
        if (descriptor.Lifetime == SvcLifetime.Singleton)
        {
            return registry.GetSingletonInstance(
                implementationType,
                () => CreateInstance(descriptor)
            );
        }
        if (descriptor.Lifetime == SvcLifetime.PerThread)
        {
            return registry.GetPerThreadInstance(
                implementationType,
                () => CreateInstance(descriptor)
            );
        }
        return CreateInstance(descriptor);
    }

    public ISvcScope CreateScope() => scopeFactory.CreateScope(this);

    private object CreateInstance(SvcDescriptor descriptor)
    {
        if (descriptor.Factory is not null)
            return descriptor.Factory(this);
        if (descriptor.ImplementationType is not null)
            return Activator.CreateInstance(descriptor.ImplementationType)!;
        throw new InvalidOperationException("No factory or implementation type found.");
    }
}
