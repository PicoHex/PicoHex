namespace PicoHex.Core.DependencyInjection;

public static partial class SvcRegistryExtensions
{
    #region Add by type

    public static ISvcRegistry AddSingleton(this ISvcRegistry registry, Type serviceType) =>
        registry.AddServiceDescriptor(
            new SvcDescriptor(serviceType, serviceType, SvcLifetime.Singleton)
        );

    public static ISvcRegistry AddSingleton(
        this ISvcRegistry registry,
        Type serviceType,
        Type implementationType
    ) =>
        registry.AddServiceDescriptor(
            new SvcDescriptor(serviceType, implementationType, SvcLifetime.Singleton)
        );

    public static ISvcRegistry AddSingleton<TService>(
        this ISvcRegistry registry,
        Type implementationType
    )
        where TService : class =>
        registry.AddServiceDescriptor(
            new SvcDescriptor(typeof(TService), implementationType, SvcLifetime.Singleton)
        );

    public static ISvcRegistry AddSingleton<TService>(this ISvcRegistry registry)
        where TService : class =>
        registry.AddServiceDescriptor(
            new SvcDescriptor(typeof(TService), typeof(TService), SvcLifetime.Singleton)
        );

    public static ISvcRegistry AddSingleton<TService, TImplementation>(this ISvcRegistry registry)
        where TService : class
        where TImplementation : class, TService =>
        registry.AddServiceDescriptor(
            new SvcDescriptor(typeof(TService), typeof(TImplementation), SvcLifetime.Singleton)
        );

    #endregion

    #region Add by factory

    public static ISvcRegistry AddSingleton(
        this ISvcRegistry registry,
        Type serviceType,
        Func<ISvcProvider, object> factory
    ) =>
        registry.AddServiceDescriptor(
            new SvcDescriptor(serviceType, factory, SvcLifetime.Singleton)
        );

    public static ISvcRegistry AddSingleton<TService>(
        this ISvcRegistry registry,
        Func<ISvcProvider, TService> factory
    )
        where TService : class =>
        registry.AddServiceDescriptor(
            new SvcDescriptor(typeof(TService), factory, SvcLifetime.Singleton)
        );

    public static ISvcRegistry AddSingleton<TService, TImplementation>(
        this ISvcRegistry registry,
        Func<ISvcProvider, TImplementation> factory
    )
        where TService : class
        where TImplementation : class, TService =>
        registry.AddServiceDescriptor(
            new SvcDescriptor(typeof(TService), factory, SvcLifetime.Singleton)
        );

    #endregion
}
