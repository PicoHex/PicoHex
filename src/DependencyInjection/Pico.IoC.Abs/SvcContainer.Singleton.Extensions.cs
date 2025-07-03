namespace Pico.DI.Abs;

public static partial class SvcContainerExtensions
{
    #region Add by instance

    public static ISvcContainer RegisterSingle(
        this ISvcContainer container,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
            Type serviceType,
        object instance
    ) => container.Register(new SvcDescriptor(serviceType, instance));

    public static ISvcContainer RegisterSingle<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TService
    >(this ISvcContainer container, object instance) =>
        container.Register(new SvcDescriptor(typeof(TService), instance));

    #endregion

    #region Add by type

    public static ISvcContainer RegisterSingle(
        this ISvcContainer container,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
            Type serviceType
    ) => container.Register(serviceType, SvcLifetime.Singleton);

    public static ISvcContainer RegisterSingle(
        this ISvcContainer container,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
            Type serviceType,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
            Type implementationType
    ) => container.Register(serviceType, implementationType, SvcLifetime.Singleton);

    public static ISvcContainer RegisterSingle<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TService
    >(
        this ISvcContainer container,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
            Type implementationType
    ) => container.Register<TService>(implementationType, SvcLifetime.Singleton);

    public static ISvcContainer RegisterSingle<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TService
    >(this ISvcContainer container) => container.Register<TService>(SvcLifetime.Singleton);

    public static ISvcContainer RegisterSingle<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TService,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
            TImplementation
    >(this ISvcContainer container)
        where TImplementation : TService =>
        container.Register<TService, TImplementation>(SvcLifetime.Singleton);

    #endregion

    #region Add by factory

    public static ISvcContainer RegisterSingle(
        this ISvcContainer container,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
            Type serviceType,
        Func<ISvcProvider, object> factory
    ) => container.Register(serviceType, factory, SvcLifetime.Singleton);

    public static ISvcContainer RegisterSingle<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TService
    >(this ISvcContainer container, Func<ISvcProvider, TService> factory)
        where TService : class => container.Register(factory, SvcLifetime.Singleton);

    public static ISvcContainer RegisterSingle<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TService,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
            TImplementation
    >(this ISvcContainer container, Func<ISvcProvider, TImplementation> factory)
        where TService : class
        where TImplementation : class, TService =>
        container.Register<TService>(factory, SvcLifetime.Singleton);

    #endregion
}
