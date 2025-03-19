namespace PicoHex.IoC.Abstractions;

public static partial class SvcRegistryExtensions
{
    #region Add by type

    public static ISvcContainer RegisterSingle(
        this ISvcContainer registry,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
            Type serviceType
    ) => registry.Register(serviceType, SvcLifetime.Singleton);

    public static ISvcContainer RegisterSingle(
        this ISvcContainer registry,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
            Type serviceType,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
            Type implementationType
    ) => registry.Register(serviceType, implementationType, SvcLifetime.Singleton);

    public static ISvcContainer RegisterSingle<TService>(
        this ISvcContainer registry,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
            Type implementationType
    ) => registry.Register<TService>(implementationType, SvcLifetime.Singleton);

    public static ISvcContainer RegisterSingle<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TService
    >(this ISvcContainer registry) => registry.Register<TService>(SvcLifetime.Singleton);

    public static ISvcContainer RegisterSingle<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TService,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
            TImplementation
    >(this ISvcContainer registry)
        where TImplementation : TService =>
        registry.Register<TService, TImplementation>(SvcLifetime.Singleton);

    #endregion

    #region Add by factory

    public static ISvcContainer RegisterSingle(
        this ISvcContainer registry,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
            Type serviceType,
        Func<ISvcProvider, object> factory
    ) => registry.Register(serviceType, factory, SvcLifetime.Singleton);

    public static ISvcContainer RegisterSingle<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TService
    >(this ISvcContainer registry, Func<ISvcProvider, TService> factory)
        where TService : class => registry.Register(factory, SvcLifetime.Singleton);

    public static ISvcContainer RegisterSingle<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TService,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
            TImplementation
    >(this ISvcContainer registry, Func<ISvcProvider, TImplementation> factory)
        where TService : class
        where TImplementation : class, TService =>
        registry.Register<TService>(factory, SvcLifetime.Singleton);

    #endregion
}
