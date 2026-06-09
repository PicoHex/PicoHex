namespace PicoMediator.DI;

/// <summary>
/// DI registration extensions for PicoMediator.
/// </summary>
public static class SvcContainerMediatorExtensions
{
    extension(ISvcContainer container)
    {
        /// <summary>
        /// Registers <see cref="Mediator"/> as <see cref="IMediator"/> with the specified lifetime.
        /// Default is <see cref="SvcLifetime.Scoped"/> for request-scoped isolation.
        /// Use <see cref="SvcLifetime.Singleton"/> when the mediator is stateless
        /// and no per-request mediator configuration is needed.
        /// The source generator (PicoMediator.Gen) must be referenced for
        /// compile-time request dispatch optimization.
        /// </summary>
        /// <param name="lifetime">The service lifetime for the mediator registration. Default: Scoped.</param>
        public ISvcContainer AddPicoMediator(SvcLifetime lifetime = SvcLifetime.Scoped)
        {
            container.Register(
                SvcDescriptor.Create(typeof(IMediator), scope => new Mediator(scope), lifetime)
            );
            return container;
        }
    }
}
