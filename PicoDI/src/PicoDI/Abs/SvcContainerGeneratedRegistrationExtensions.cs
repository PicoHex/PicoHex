namespace PicoDI.Abs;

/// <summary>
/// Provides source-generator marker registration methods for <see cref="ISvcContainer"/>.
/// These methods intentionally fail fast until the container's generated-configuration state is marked as applied.
/// </summary>
public static class SvcContainerGeneratedRegistrationExtensions
{
    extension(ISvcContainer container)
    {
        /// <summary>
        /// Registers a service with the specified implementation type and lifetime.
        /// This is a placeholder for source generator to provide the actual implementation.
        /// </summary>
        public ISvcContainer Register<TService, TImplementation>(SvcLifetime lifetime)
            where TImplementation : TService
        {
            SvcContainerRegistrationGuards.EnsureGeneratedRegistrationsApplied(container);
            return container;
        }

        /// <summary>
        /// Registers a service type as its own implementation with the specified lifetime.
        /// This is a placeholder for source generator to provide the actual implementation.
        /// </summary>
        public ISvcContainer Register<TService>(SvcLifetime lifetime)
            where TService : class
        {
            SvcContainerRegistrationGuards.EnsureGeneratedRegistrationsApplied(container);
            return container;
        }

        /// <summary>
        /// Registers a service with the specified implementation type and lifetime.
        /// This is a placeholder for source generator to provide the actual implementation.
        /// </summary>
        public ISvcContainer Register<TService>(Type implementType, SvcLifetime lifetime)
            where TService : class
        {
            SvcContainerRegistrationGuards.EnsureGeneratedRegistrationsApplied(container);
            return container;
        }

        /// <summary>
        /// Registers a transient service with the specified implementation type.
        /// This is a placeholder for source generator to provide the actual implementation.
        /// </summary>
        public ISvcContainer RegisterTransient<TService, TImplementation>()
            where TImplementation : TService
        {
            SvcContainerRegistrationGuards.EnsureGeneratedRegistrationsApplied(container);
            return container;
        }

        /// <summary>
        /// Registers a transient service type as its own implementation.
        /// This is a placeholder for source generator to provide the actual implementation.
        /// </summary>
        public ISvcContainer RegisterTransient<TService>()
            where TService : class
        {
            SvcContainerRegistrationGuards.EnsureGeneratedRegistrationsApplied(container);
            return container;
        }

        /// <summary>
        /// Registers a transient service with the specified implementation type.
        /// This is a placeholder for source generator to provide the actual implementation.
        /// </summary>
        public ISvcContainer RegisterTransient<TService>(Type implementType)
            where TService : class
        {
            SvcContainerRegistrationGuards.EnsureGeneratedRegistrationsApplied(container);
            return container;
        }

        /// <summary>
        /// Registers a scoped service with the specified implementation type.
        /// This is a placeholder for source generator to provide the actual implementation.
        /// </summary>
        public ISvcContainer RegisterScoped<TService, TImplementation>()
            where TImplementation : TService
        {
            SvcContainerRegistrationGuards.EnsureGeneratedRegistrationsApplied(container);
            return container;
        }

        /// <summary>
        /// Registers a scoped service type as its own implementation.
        /// This is a placeholder for source generator to provide the actual implementation.
        /// </summary>
        public ISvcContainer RegisterScoped<TService>()
            where TService : class
        {
            SvcContainerRegistrationGuards.EnsureGeneratedRegistrationsApplied(container);
            return container;
        }

        /// <summary>
        /// Registers a scoped service with the specified implementation type.
        /// This is a placeholder for source generator to provide the actual implementation.
        /// </summary>
        public ISvcContainer RegisterScoped<TService>(Type implementType)
            where TService : class
        {
            SvcContainerRegistrationGuards.EnsureGeneratedRegistrationsApplied(container);
            return container;
        }

        /// <summary>
        /// Registers a singleton service with the specified implementation type.
        /// This is a placeholder for source generator to provide the actual implementation.
        /// </summary>
        public ISvcContainer RegisterSingleton<TService, TImplementation>()
            where TImplementation : TService
        {
            SvcContainerRegistrationGuards.EnsureGeneratedRegistrationsApplied(container);
            return container;
        }

        /// <summary>
        /// Registers a singleton service type as its own implementation.
        /// This is a placeholder for source generator to provide the actual implementation.
        /// </summary>
        public ISvcContainer RegisterSingleton<TService>()
            where TService : class
        {
            SvcContainerRegistrationGuards.EnsureGeneratedRegistrationsApplied(container);
            return container;
        }

        /// <summary>
        /// Registers a singleton service with the specified implementation type.
        /// This is a placeholder for source generator to provide the actual implementation.
        /// </summary>
        public ISvcContainer RegisterSingleton<TService>(Type implementType)
            where TService : class
        {
            SvcContainerRegistrationGuards.EnsureGeneratedRegistrationsApplied(container);
            return container;
        }
    }
}
