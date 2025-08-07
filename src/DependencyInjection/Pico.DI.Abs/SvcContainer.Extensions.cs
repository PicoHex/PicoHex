namespace Pico.DI.Abs;

public static partial class SvcContainerExtensions
{
    #region Add by type

    public static ISvcContainer Register(
        this ISvcContainer registry,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
            Type serviceType,
        SvcLifetime lifecycle
    ) => registry.Register(new SvcDescriptor(serviceType, lifecycle));

    public static ISvcContainer Register(
        this ISvcContainer registry,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
            Type serviceType,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
            Type implementationType,
        SvcLifetime lifecycle
    ) => registry.Register(new SvcDescriptor(serviceType, implementationType, lifecycle));

    public static ISvcContainer Register<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TService
    >(
        this ISvcContainer registry,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
            Type implementationType,
        SvcLifetime lifecycle
    ) => registry.Register(typeof(TService), implementationType, lifecycle);

    public static ISvcContainer Register<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TService
    >(this ISvcContainer registry, SvcLifetime lifecycle) =>
        registry.Register(typeof(TService), lifecycle);

    public static ISvcContainer Register<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TService,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
            TImplementation
    >(this ISvcContainer registry, SvcLifetime lifecycle)
        where TImplementation : TService =>
        registry.Register(typeof(TService), typeof(TImplementation), lifecycle);

    #endregion

    #region Add by factory

    public static ISvcContainer Register(
        this ISvcContainer registry,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
            Type serviceType,
        Func<ISvcProvider, object> factory,
        SvcLifetime lifetime
    ) => registry.Register(new SvcDescriptor(serviceType, factory, lifetime));

    public static ISvcContainer Register<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TService
    >(this ISvcContainer registry, Func<ISvcProvider, TService> factory, SvcLifetime lifetime)
        where TService : class => registry.Register(typeof(TService), factory, lifetime);

    public static ISvcContainer Register<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TService,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
            TImplementation
    >(
        this ISvcContainer registry,
        Func<ISvcProvider, TImplementation> factory,
        SvcLifetime lifetime
    )
        where TService : class
        where TImplementation : class, TService => registry.Register<TService>(factory, lifetime);

    #endregion
}
