namespace PicoMediator.DI;

/// <summary>
/// DI registration extensions for PicoMediator.
/// </summary>
public static class SvcContainerMediatorExtensions
{
    extension(ISvcContainer container)
    {
        /// <summary>
        /// Registers <see cref="Mediator"/> as <see cref="IMediator"/> with <see cref="SvcLifetime.Scoped"/>.
        /// The source generator (PicoMediator.Gen) must be referenced for
        /// compile-time request dispatch optimization.
        /// </summary>
        public ISvcContainer AddPicoMediator()
        {
            container.RegisterScoped<IMediator>(scope => new Mediator(scope));
            return container;
        }
    }
}
