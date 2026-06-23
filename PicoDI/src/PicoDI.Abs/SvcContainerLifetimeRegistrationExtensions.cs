namespace PicoDI.Abs;

/// <summary>
/// Provides lifetime-specific runtime registration extensions for <see cref="ISvcContainer"/>.
/// All public methods delegate to the internal core helpers below so that the throw-site
/// and open-generic gate logic live in exactly two places.
/// </summary>
public static class SvcContainerLifetimeRegistrationExtensions
{
    // ── Core helpers (internal, shared within assembly) ──

    internal static ISvcContainer RegisterTypeBased(
        this ISvcContainer container,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.Interfaces)] Type serviceType,
        Type implementType,
        SvcLifetime lifetime
    ) => container.Register(new SvcDescriptor(serviceType, implementType, lifetime));

    internal static ISvcContainer RegisterSelfType(
        this ISvcContainer container,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.Interfaces)] Type serviceType,
        SvcLifetime lifetime
    ) => container.Register(new SvcDescriptor(serviceType, serviceType, lifetime));

    internal static ISvcContainer RegisterFactory(
        this ISvcContainer container,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.Interfaces)] Type serviceType,
        Func<ISvcScope, object> factory,
        SvcLifetime lifetime
    ) => container.Register(new SvcDescriptor(serviceType, factory, lifetime));

    internal static ISvcContainer RegisterSingleInstance(
        this ISvcContainer container,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.Interfaces)] Type serviceType,
        object instance
    ) => container.Register(SvcDescriptor.FromInstance(serviceType, instance));

    // ── Public thin wrappers ──

    extension(ISvcContainer container)
    {
        // ── Transient ──
        public ISvcContainer RegisterTransient(
            [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.Interfaces)]
                Type serviceType,
            Type implementType
        ) => container.RegisterTypeBased(serviceType, implementType, SvcLifetime.Transient);

        public ISvcContainer RegisterTransient(
            [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.Interfaces)] Type serviceType
        ) => container.RegisterSelfType(serviceType, SvcLifetime.Transient);

        public ISvcContainer RegisterTransient<TService>(Func<ISvcScope, TService> factory)
            where TService : class =>
            container.RegisterFactory(typeof(TService), factory, SvcLifetime.Transient);

        public ISvcContainer RegisterTransient<TService, TImplementation>(
            Func<ISvcScope, TImplementation> factory
        )
            where TService : class
            where TImplementation : class =>
            container.RegisterFactory(typeof(TService), factory, SvcLifetime.Transient);

        // ── Scoped ──
        public ISvcContainer RegisterScoped(
            [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.Interfaces)]
                Type serviceType,
            Type implementType
        ) => container.RegisterTypeBased(serviceType, implementType, SvcLifetime.Scoped);

        public ISvcContainer RegisterScoped(
            [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.Interfaces)] Type serviceType
        ) => container.RegisterSelfType(serviceType, SvcLifetime.Scoped);

        public ISvcContainer RegisterScoped<TService>(Func<ISvcScope, TService> factory)
            where TService : class =>
            container.RegisterFactory(typeof(TService), factory, SvcLifetime.Scoped);

        public ISvcContainer RegisterScoped<TService, TImplementation>(
            Func<ISvcScope, TImplementation> factory
        )
            where TService : class
            where TImplementation : class =>
            container.RegisterFactory(typeof(TService), factory, SvcLifetime.Scoped);

        // ── Singleton (factory) ──
        public ISvcContainer RegisterSingleton(
            [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.Interfaces)]
                Type serviceType,
            Type implementType
        ) => container.RegisterTypeBased(serviceType, implementType, SvcLifetime.Singleton);

        public ISvcContainer RegisterSingleton(
            [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.Interfaces)] Type serviceType
        ) => container.RegisterSelfType(serviceType, SvcLifetime.Singleton);

        public ISvcContainer RegisterSingleton<TService>(Func<ISvcScope, TService> factory)
            where TService : class =>
            container.RegisterFactory(typeof(TService), factory, SvcLifetime.Singleton);

        public ISvcContainer RegisterSingleton<TService, TImplementation>(
            Func<ISvcScope, TImplementation> factory
        )
            where TService : class
            where TImplementation : class =>
            container.RegisterFactory(typeof(TService), factory, SvcLifetime.Singleton);

        // ── RegisterSingle (pre-built instance, always Singleton) ──
        public ISvcContainer RegisterSingle(
            [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.Interfaces)]
                Type serviceType,
            object instance
        )
        {
            if (instance is null)
                throw new ArgumentNullException(nameof(instance));
            return container.RegisterSingleInstance(serviceType, instance);
        }

        public ISvcContainer RegisterSingle<TService>(TService instance)
            where TService : class
        {
            if (instance is null)
                throw new ArgumentNullException(nameof(instance));
            return container.RegisterSingleInstance(typeof(TService), instance);
        }
    }
}
