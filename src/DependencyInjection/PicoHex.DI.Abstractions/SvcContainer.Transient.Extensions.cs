namespace PicoHex.DI.Abstractions;

public static partial class SvcContainerExtensions
{
    #region Add by type

    public static ISvcContainer RegisterTransient(
        this ISvcContainer container,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
            Type serviceType
    ) => container.Register(serviceType, SvcLifetime.Transient);

    public static ISvcContainer RegisterTransient(
        this ISvcContainer container,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
            Type serviceType,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
            Type implementationType
    ) => container.Register(serviceType, implementationType, SvcLifetime.Transient);

    public static ISvcContainer RegisterTransient<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TService
    >(
        this ISvcContainer container,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
            Type implementationType
    ) => container.Register<TService>(implementationType, SvcLifetime.Transient);

    public static ISvcContainer RegisterTransient<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TService
    >(this ISvcContainer container) => container.Register<TService>(SvcLifetime.Transient);

    public static ISvcContainer RegisterTransient<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TService,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
            TImplementation
    >(this ISvcContainer container)
        where TImplementation : TService =>
        container.Register<TService, TImplementation>(SvcLifetime.Transient);

    #endregion

    #region Add by factory

    public static ISvcContainer RegisterTransient(
        this ISvcContainer container,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
            Type serviceType,
        Func<ISvcResolver, object> factory
    ) => container.Register(serviceType, factory, SvcLifetime.Transient);

    public static ISvcContainer RegisterTransient<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TService
    >(this ISvcContainer container, Func<ISvcResolver, TService> factory)
        where TService : class => container.Register(factory, SvcLifetime.Transient);

    public static ISvcContainer RegisterTransient<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TService,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
            TImplementation
    >(this ISvcContainer container, Func<ISvcResolver, TImplementation> factory)
        where TService : class
        where TImplementation : class, TService =>
        container.Register<TService>(factory, SvcLifetime.Transient);

    #endregion
}
