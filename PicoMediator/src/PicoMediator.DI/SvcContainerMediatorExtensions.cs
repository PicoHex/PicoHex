namespace PicoMediator.DI;

/// <summary>
/// DI registration extensions for PicoMediator.
/// </summary>
public static class SvcContainerMediatorExtensions
{
    extension(ISvcContainer container)
    {
        /// <summary>
        /// Registers <see cref="Mediator"/> as <see cref="IMediator"/> with the
        /// specified lifetime. Default is <see cref="SvcLifetime.Scoped"/> for
        /// request-scoped isolation. Use <see cref="SvcLifetime.Singleton"/> when
        /// the mediator is stateless and no per-request mediator configuration is
        /// needed.
        /// When <paramref name="autoRegisterHandlers"/> is true (default), all
        /// handler implementations scanned by PicoMediator.Gen are registered
        /// automatically (declare-and-subscribe). Manual registrations made
        /// before this call win over generated ones.
        /// </summary>
        public ISvcContainer AddPicoMediator(
            SvcLifetime lifetime = SvcLifetime.Scoped,
            bool autoRegisterHandlers = true
        )
        {
            if (autoRegisterHandlers)
                MediatorAutoSubscriptionRegistry.TryApplyConfiguration(container);

            container.Register(
                SvcDescriptor.Create(typeof(IMediator), scope => new Mediator(scope), lifetime)
            );
            return container;
        }
    }
}
