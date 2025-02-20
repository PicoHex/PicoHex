namespace PicoHex.Core.DependencyInjection;

public static partial class SvcRegistryExtensions
{
    #region Add by type

    public static ISvcRegistry AddService(
        this ISvcRegistry registry,
        Type serviceType,
        SvcLifetime lifetime
    ) => registry.AddServiceDescriptor(new SvcDescriptor(serviceType, serviceType, lifetime));

    public static ISvcRegistry AddService(
        this ISvcRegistry registry,
        Type serviceType,
        Type implementationType,
        SvcLifetime lifetime
    ) =>
        registry.AddServiceDescriptor(new SvcDescriptor(serviceType, implementationType, lifetime));

    public static ISvcRegistry AddService<TService>(
        this ISvcRegistry registry,
        Type implementationType,
        SvcLifetime lifetime
    )
        where TService : class =>
        registry.AddServiceDescriptor(
            new SvcDescriptor(typeof(TService), implementationType, lifetime)
        );

    public static ISvcRegistry AddService<TService>(
        this ISvcRegistry registry,
        SvcLifetime lifetime
    )
        where TService : class =>
        registry.AddServiceDescriptor(
            new SvcDescriptor(typeof(TService), typeof(TService), lifetime)
        );

    public static ISvcRegistry AddService<TService, TImplementation>(
        this ISvcRegistry registry,
        SvcLifetime lifetime
    )
        where TService : class
        where TImplementation : class, TService =>
        registry.AddServiceDescriptor(
            new SvcDescriptor(typeof(TService), typeof(TImplementation), lifetime)
        );

    #endregion

    #region Add by factory

    public static ISvcRegistry AddService(
        this ISvcRegistry registry,
        Type serviceType,
        Func<ISvcProvider, object> factory,
        SvcLifetime lifetime
    ) => registry.AddServiceDescriptor(new SvcDescriptor(serviceType, factory, lifetime));

    public static ISvcRegistry AddService<TService>(
        this ISvcRegistry registry,
        Func<ISvcProvider, TService> factory,
        SvcLifetime lifetime
    )
        where TService : class =>
        registry.AddServiceDescriptor(new SvcDescriptor(typeof(TService), factory, lifetime));

    public static ISvcRegistry AddService<TService, TImplementation>(
        this ISvcRegistry registry,
        Func<ISvcProvider, TImplementation> factory,
        SvcLifetime lifetime
    )
        where TService : class
        where TImplementation : class, TService =>
        registry.AddServiceDescriptor(new SvcDescriptor(typeof(TService), factory, lifetime));

    #endregion
}
