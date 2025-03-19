namespace PicoHex.IoC.Abstractions;

public static partial class SvcRegistryExtensions
{
    #region Add by type

    public static ISvcContainer RegisterScoped(
        this ISvcContainer registry,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
            Type serviceType
    ) => registry.Register(serviceType, SvcLifetime.Scoped);

    public static ISvcContainer RegisterScoped(
        this ISvcContainer registry,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
            Type serviceType,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
            Type implementationType
    ) => registry.Register(serviceType, implementationType, SvcLifetime.Scoped);

    public static ISvcContainer RegisterScoped<TService>(
        this ISvcContainer registry,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
            Type implementationType
    ) => registry.Register<TService>(implementationType, SvcLifetime.Scoped);

    public static ISvcContainer RegisterScoped<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TService
    >(this ISvcContainer registry) => registry.Register<TService>(SvcLifetime.Scoped);

    public static ISvcContainer RegisterScoped<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TService,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
            TImplementation
    >(this ISvcContainer registry)
        where TImplementation : TService =>
        registry.Register<TService, TImplementation>(SvcLifetime.Scoped);

    #endregion

    #region Add by factory

    public static ISvcContainer RegisterScoped(
        this ISvcContainer registry,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
            Type serviceType,
        Func<ISvcProvider, object> factory
    ) => registry.Register(serviceType, factory, SvcLifetime.Scoped);

    public static ISvcContainer RegisterScoped<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TService
    >(this ISvcContainer registry, Func<ISvcProvider, TService> factory)
        where TService : class => registry.Register(factory, SvcLifetime.Scoped);

    public static ISvcContainer RegisterScoped<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TService,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
            TImplementation
    >(this ISvcContainer registry, Func<ISvcProvider, TImplementation> factory)
        where TService : class
        where TImplementation : class, TService =>
        registry.Register<TService>(factory, SvcLifetime.Scoped);

    #endregion
}
