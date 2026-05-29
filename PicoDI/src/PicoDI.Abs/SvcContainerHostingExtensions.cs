namespace PicoDI.Abs;

/// <summary>
/// Provides hosted service registration extensions for <see cref="ISvcContainer"/>.
/// </summary>
public static class SvcContainerHostingExtensions
{
    extension(ISvcContainer container)
    {
        /// <summary>
        /// Registers a hosted service type as a singleton.
        /// </summary>
        /// <typeparam name="THostedSvc">The hosted service type to register.</typeparam>
        /// <returns>The <see cref="ISvcContainer"/> instance for chaining.</returns>
        public ISvcContainer RegisterHostedSvc<THostedSvc>()
            where THostedSvc : class, IHostedSvc =>
            container.Register(new SvcDescriptor(typeof(THostedSvc), typeof(THostedSvc), SvcLifetime.Singleton));

        /// <summary>
        /// Registers a hosted service type with a factory delegate as a singleton.
        /// </summary>
        /// <typeparam name="THostedSvc">The hosted service type to register.</typeparam>
        /// <param name="factory">The factory delegate to create the hosted service instance.</param>
        /// <returns>The <see cref="ISvcContainer"/> instance for chaining.</returns>
        public ISvcContainer RegisterHostedSvc<THostedSvc>(Func<ISvcScope, THostedSvc> factory)
            where THostedSvc : class, IHostedSvc =>
            container.Register(
                SvcDescriptor.Create(typeof(THostedSvc), scope => factory(scope)!, SvcLifetime.Singleton));

        /// <summary>
        /// Registers a hosted service type as a singleton.
        /// </summary>
        /// <param name="hostedServiceType">The hosted service type to register.</param>
        /// <returns>The <see cref="ISvcContainer"/> instance for chaining.</returns>
        /// <exception cref="HostedSvcRegistrationException">Thrown when <paramref name="hostedServiceType"/> does not implement <see cref="IHostedSvc"/>.</exception>
        public ISvcContainer RegisterHostedSvc(
            [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.Interfaces)] Type hostedServiceType)
        {
            if (!typeof(IHostedSvc).IsAssignableFrom(hostedServiceType))
                throw new HostedSvcRegistrationException(
                    $"Type '{hostedServiceType.FullName}' does not implement {nameof(IHostedSvc)}.");

            return container.Register(new SvcDescriptor(hostedServiceType, hostedServiceType, SvcLifetime.Singleton));
        }

        /// <summary>
        /// Registers a hosted service type with a factory delegate as a singleton.
        /// </summary>
        /// <param name="hostedServiceType">The hosted service type to register.</param>
        /// <param name="factory">The factory delegate to create the hosted service instance.</param>
        /// <returns>The <see cref="ISvcContainer"/> instance for chaining.</returns>
        /// <exception cref="HostedSvcRegistrationException">Thrown when <paramref name="hostedServiceType"/> does not implement <see cref="IHostedSvc"/>.</exception>
        public ISvcContainer RegisterHostedSvc(
            [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.Interfaces)] Type hostedServiceType,
            Func<ISvcScope, object> factory)
        {
            if (!typeof(IHostedSvc).IsAssignableFrom(hostedServiceType))
                throw new HostedSvcRegistrationException(
                    $"Type '{hostedServiceType.FullName}' does not implement {nameof(IHostedSvc)}.");

            return container.Register(new SvcDescriptor(hostedServiceType, factory, SvcLifetime.Singleton));
        }
    }
}
