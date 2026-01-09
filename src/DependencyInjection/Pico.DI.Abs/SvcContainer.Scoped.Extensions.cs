namespace Pico.DI.Abs;

public static partial class SvcContainerExtensions
{
    #region Add by type

    extension(ISvcContainer container)
    {
        public ISvcContainer RegisterScoped([DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
            Type serviceType
        ) => container.Register(serviceType, SvcLifetime.Scoped);

        public ISvcContainer RegisterScoped([DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
            Type serviceType,
            [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
            Type implementationType
        ) => container.Register(serviceType, implementationType, SvcLifetime.Scoped);

        public ISvcContainer RegisterScoped<
            [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TService
        >([DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
            Type implementationType
        ) => container.Register<TService>(implementationType, SvcLifetime.Scoped);

        public ISvcContainer RegisterScoped<
            [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TService
        >() => container.Register<TService>(SvcLifetime.Scoped);

        public ISvcContainer RegisterScoped<
            [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TService,
            [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
            TImplementation
        >()
            where TImplementation : TService =>
            container.Register<TService, TImplementation>(SvcLifetime.Scoped);
    }

    #endregion

    #region Add by factory

    extension(ISvcContainer container)
    {
        public ISvcContainer RegisterScoped([DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
            Type serviceType,
            Func<ISvcProvider, object> factory
        ) => container.Register(serviceType, factory, SvcLifetime.Scoped);

        public ISvcContainer RegisterScoped<
            [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TService
        >(Func<ISvcProvider, TService> factory)
            where TService : class => container.Register(factory, SvcLifetime.Scoped);

        public ISvcContainer RegisterScoped<
            [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TService,
            [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
            TImplementation
        >(Func<ISvcProvider, TImplementation> factory)
            where TService : class
            where TImplementation : class, TService =>
            container.Register<TService>(factory, SvcLifetime.Scoped);
    }

    #endregion
}
