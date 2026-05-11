namespace PicoDI.Abs;

/// <summary>
/// Provides general-purpose runtime registration extensions for <see cref="ISvcContainer"/>.
/// Type-based and factory-based overloads delegate to the shared core helpers in
/// <see cref="SvcContainerLifetimeRegistrationExtensions"/> so that the open-generic
/// gate and throw-site logic have a single source of truth.
/// </summary>
public static class SvcContainerGeneralRegistrationExtensions
{
    extension(ISvcContainer container)
    {
        /// <summary>
        /// Registers a service with the specified implementation type and lifetime.
        /// Supports runtime registration only for open generic definitions.
        /// </summary>
        public ISvcContainer Register(Type serviceType, Type implementType, SvcLifetime lifetime) =>
            container.RegisterTypeBased(serviceType, implementType, lifetime);

        /// <summary>
        /// Registers a service type as its own implementation with the specified lifetime.
        /// Supports runtime registration only for open generic definitions.
        /// </summary>
        public ISvcContainer Register(Type serviceType, SvcLifetime lifetime) =>
            container.RegisterSelfType(serviceType, lifetime);

        /// <summary>
        /// Registers a service with a factory function and specified lifetime.
        /// </summary>
        public ISvcContainer Register(
            Type serviceType,
            Func<ISvcScope, object> factory,
            SvcLifetime lifetime
        ) => container.RegisterFactory(serviceType, factory, lifetime);

        /// <summary>
        /// Registers a service with a factory function and specified lifetime.
        /// </summary>
        public ISvcContainer Register<TService>(Func<ISvcScope, TService> factory, SvcLifetime lifetime)
            where TService : class =>
            container.RegisterFactory(typeof(TService), factory, lifetime);

        /// <summary>
        /// Registers a service with a factory function and specified lifetime.
        /// </summary>
        public ISvcContainer Register<TService, TImplementation>(
            Func<ISvcScope, TImplementation> factory,
            SvcLifetime lifetime
        )
            where TService : class
            where TImplementation : class =>
            container.RegisterFactory(typeof(TService), factory, lifetime);

        /// <summary>
        /// Registers multiple service descriptors at once.
        /// </summary>
        public ISvcContainer RegisterRange(IEnumerable<SvcDescriptor> descriptors)
        {
            if (descriptors is null)
                throw new ArgumentNullException(nameof(descriptors));
            foreach (var descriptor in descriptors)
            {
                container.Register(descriptor);
            }

            return container;
        }
    }
}
