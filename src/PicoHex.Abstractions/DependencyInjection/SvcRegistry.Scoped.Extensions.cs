namespace PicoHex.Abstractions.DependencyInjection;

public static partial class SvcRegistryExtensions
{
    #region Add by type

    public static ISvcRegistry AddScope(this ISvcRegistry registry, Type serviceType) =>
        registry.AddServiceDescriptor(
            new SvcDescriptor(serviceType, serviceType, SvcLifetime.Scoped)
        );

    public static ISvcRegistry AddScope(
        this ISvcRegistry registry,
        Type serviceType,
        Type implementationType
    ) =>
        registry.AddServiceDescriptor(
            new SvcDescriptor(serviceType, implementationType, SvcLifetime.Scoped)
        );

    public static ISvcRegistry AddScope<TService>(
        this ISvcRegistry registry,
        Type implementationType
    )
        where TService : class =>
        registry.AddServiceDescriptor(
            new SvcDescriptor(typeof(TService), implementationType, SvcLifetime.Scoped)
        );

    public static ISvcRegistry AddScope<TService>(this ISvcRegistry registry)
        where TService : class =>
        registry.AddServiceDescriptor(
            new SvcDescriptor(typeof(TService), typeof(TService), SvcLifetime.Scoped)
        );

    public static ISvcRegistry AddScope<TService, TImplementation>(this ISvcRegistry registry)
        where TService : class
        where TImplementation : class, TService =>
        registry.AddServiceDescriptor(
            new SvcDescriptor(typeof(TService), typeof(TImplementation), SvcLifetime.Scoped)
        );

    #endregion

    #region Add by factory

    public static ISvcRegistry AddScope(
        this ISvcRegistry registry,
        Type serviceType,
        Func<ISvcProvider, object> factory
    ) => registry.AddServiceDescriptor(new SvcDescriptor(serviceType, factory, SvcLifetime.Scoped));

    public static ISvcRegistry AddScope<TService>(
        this ISvcRegistry registry,
        Func<ISvcProvider, TService> factory
    )
        where TService : class =>
        registry.AddServiceDescriptor(
            new SvcDescriptor(typeof(TService), factory, SvcLifetime.Scoped)
        );

    public static ISvcRegistry AddScope<TService, TImplementation>(
        this ISvcRegistry registry,
        Func<ISvcProvider, TImplementation> factory
    )
        where TService : class
        where TImplementation : class, TService =>
        registry.AddServiceDescriptor(
            new SvcDescriptor(typeof(TService), factory, SvcLifetime.Scoped)
        );

    #endregion
}
