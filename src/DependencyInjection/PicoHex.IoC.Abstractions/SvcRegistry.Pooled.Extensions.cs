namespace PicoHex.IoC.Abstractions;

public static partial class SvcRegistryExtensions
{
    #region Add by type

    public static ISvcContainer RegisterPooled(
        this ISvcContainer container,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
            Type serviceType
    ) => container.Register(serviceType, SvcLifetime.Pooled);

    public static ISvcContainer RegisterPooled(
        this ISvcContainer container,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
            Type serviceType,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
            Type implementationType
    ) => container.Register(serviceType, implementationType, SvcLifetime.Pooled);

    public static ISvcContainer RegisterPooled<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TService
    >(
        this ISvcContainer container,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
            Type implementationType
    ) => container.Register<TService>(implementationType, SvcLifetime.Pooled);

    public static ISvcContainer RegisterPooled<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TService
    >(this ISvcContainer container) => container.Register<TService>(SvcLifetime.Pooled);

    public static ISvcContainer RegisterPooled<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TService,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
            TImplementation
    >(this ISvcContainer container)
        where TImplementation : TService =>
        container.Register<TService, TImplementation>(SvcLifetime.Pooled);

    #endregion

    #region Add by factory

    public static ISvcContainer RegisterPooled(
        this ISvcContainer container,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
            Type serviceType,
        Func<ISvcProvider, object> factory
    ) => container.Register(serviceType, factory, SvcLifetime.Pooled);

    public static ISvcContainer RegisterPooled<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TService
    >(this ISvcContainer container, Func<ISvcProvider, TService> factory)
        where TService : class => container.Register(factory, SvcLifetime.Pooled);

    public static ISvcContainer RegisterPooled<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TService,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
            TImplementation
    >(this ISvcContainer container, Func<ISvcProvider, TImplementation> factory)
        where TService : class
        where TImplementation : class, TService =>
        container.Register<TService>(factory, SvcLifetime.Pooled);

    #endregion
}
