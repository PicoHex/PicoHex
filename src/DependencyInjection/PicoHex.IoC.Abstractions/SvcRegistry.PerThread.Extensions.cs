namespace PicoHex.IoC.Abstractions;

public static partial class SvcRegistryExtensions
{
    #region Add by type

    public static ISvcContainer RegisterPerThread(
        this ISvcContainer container,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
            Type serviceType
    ) => container.Register(serviceType, SvcLifetime.PerThread);

    public static ISvcContainer RegisterPerThread(
        this ISvcContainer container,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
            Type serviceType,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
            Type implementationType
    ) => container.Register(serviceType, implementationType, SvcLifetime.PerThread);

    public static ISvcContainer RegisterPerThread<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TService
    >(
        this ISvcContainer container,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
            Type implementationType
    ) => container.Register<TService>(implementationType, SvcLifetime.PerThread);

    public static ISvcContainer RegisterPerThread<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TService
    >(this ISvcContainer container) => container.Register<TService>(SvcLifetime.PerThread);

    public static ISvcContainer RegisterPerThread<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TService,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
            TImplementation
    >(this ISvcContainer container)
        where TImplementation : TService =>
        container.Register<TService, TImplementation>(SvcLifetime.PerThread);

    #endregion

    #region Add by factory

    public static ISvcContainer RegisterPerThread(
        this ISvcContainer container,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
            Type serviceType,
        Func<ISvcProvider, object> factory
    ) => container.Register(serviceType, factory, SvcLifetime.PerThread);

    public static ISvcContainer RegisterPerThread<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TService
    >(this ISvcContainer container, Func<ISvcProvider, TService> factory)
        where TService : class => container.Register(factory, SvcLifetime.PerThread);

    public static ISvcContainer RegisterPerThread<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TService,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
            TImplementation
    >(this ISvcContainer container, Func<ISvcProvider, TImplementation> factory)
        where TService : class
        where TImplementation : class, TService =>
        container.Register<TService>(factory, SvcLifetime.PerThread);

    #endregion
}
