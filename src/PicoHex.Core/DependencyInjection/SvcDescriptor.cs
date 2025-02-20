namespace PicoHex.Core.DependencyInjection;

public sealed class SvcDescriptor
{
    public SvcDescriptor(Type serviceType, Type implementationType, SvcLifetime lifetime)
        : this(serviceType, lifetime)
    {
        ImplementationType = implementationType;
        var constructorInfo = implementationType
            .GetConstructors()
            .OrderByDescending(p => p.GetParameters().Length)
            .FirstOrDefault();
        if (constructorInfo is null)
            throw new InvalidOperationException(
                $"No public constructor found for type {implementationType.Name}"
            );
        Constructor = new ConstructorForResolve(constructorInfo);
    }

    public SvcDescriptor(Type serviceType, Func<ISvcProvider, object> factory, SvcLifetime lifetime)
        : this(serviceType, lifetime)
    {
        Factory = factory;
    }

    private SvcDescriptor(Type serviceType, SvcLifetime lifetime)
    {
        ServiceType = serviceType;
        Lifetime = lifetime;
    }

    public Type ServiceType { get; }
    public Func<ISvcProvider, object?>? Factory { get; }
    public Type? ImplementationType { get; }
    public ConstructorForResolve? Constructor { get; }
    public SvcLifetime Lifetime { get; }
}

public sealed class ConstructorForResolve(ConstructorInfo constructorInfo)
{
    public ConstructorInfo ConstructorInfo { get; } = constructorInfo;
    public ParameterInfo[] Parameters { get; } = constructorInfo.GetParameters();
}
