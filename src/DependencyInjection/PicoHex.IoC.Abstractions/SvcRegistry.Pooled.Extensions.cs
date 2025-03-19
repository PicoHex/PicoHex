namespace PicoHex.IoC.Abstractions;

public static partial class SvcRegistryExtensions
{
    #region Add by type

    public static ISvcContainer RegisterPooled(
        this ISvcContainer registry,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
            Type serviceType
    ) => registry.Register(serviceType, SvcLifetime.Pooled);

    public static ISvcContainer RegisterPooled(
        this ISvcContainer registry,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
            Type serviceType,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
            Type implementationType
    ) => registry.Register(serviceType, implementationType, SvcLifetime.Pooled);

    public static ISvcContainer RegisterPooled<TService>(
        this ISvcContainer registry,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
            Type implementationType
    ) => registry.Register<TService>(implementationType, SvcLifetime.Pooled);

    public static ISvcContainer RegisterPooled<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TService
    >(this ISvcContainer registry) => registry.Register<TService>(SvcLifetime.Pooled);

    public static ISvcContainer RegisterPooled<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TService,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
            TImplementation
    >(this ISvcContainer registry)
        where TImplementation : TService =>
        registry.Register<TService, TImplementation>(SvcLifetime.Pooled);

    #endregion

    #region Add by factory

    public static ISvcContainer RegisterPooled(
        this ISvcContainer registry,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
            Type serviceType,
        Func<ISvcProvider, object> factory
    ) => registry.Register(serviceType, factory, SvcLifetime.Pooled);

    public static ISvcContainer RegisterPooled<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TService
    >(this ISvcContainer registry, Func<ISvcProvider, TService> factory)
        where TService : class => registry.Register(factory, SvcLifetime.Pooled);

    public static ISvcContainer RegisterPooled<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TService,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
            TImplementation
    >(this ISvcContainer registry, Func<ISvcProvider, TImplementation> factory)
        where TService : class
        where TImplementation : class, TService =>
        registry.Register<TService>(factory, SvcLifetime.Pooled);

    #endregion
}
