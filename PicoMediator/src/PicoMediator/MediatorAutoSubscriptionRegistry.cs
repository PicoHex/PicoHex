namespace PicoMediator;

/// <summary>
/// Coordinates source-generated handler registrations for PicoMediator.
/// Thin public wrapper over the shared <see cref="PicoDI.Abs.GeneratorConfiguratorRegistry"/>
/// (also used by PicoDI): generated code registers configurators by stable id;
/// AddPicoMediator() applies them to a container exactly once.
/// </summary>
public static class MediatorAutoSubscriptionRegistry
{
    /// <summary>
    /// Registers a configurator using a stable identifier so repeated module
    /// initializers replace the existing registration instead of appending a
    /// duplicate. Configurators are applied in deterministic
    /// <see cref="StringComparer.Ordinal"/> order of this identifier.
    /// Intended for PicoMediator.Gen generated code.
    /// </summary>
    public static void Register(string configuratorId, Action<ISvcContainer> configurator) =>
        PicoDI.Abs.GeneratorConfiguratorRegistry.Register(configuratorId, configurator);

    /// <summary>
    /// Applies all registered configurators to the given container exactly
    /// once. If any configurator throws, the container is NOT marked as
    /// applied, allowing retries on subsequent calls.
    /// </summary>
    public static bool TryApplyConfiguration(ISvcContainer container) =>
        PicoDI.Abs.GeneratorConfiguratorRegistry.TryApply(container);

    /// <summary>Clears all registered configurators to enable isolated test execution.</summary>
    internal static void ClearForTesting() => PicoDI.Abs.GeneratorConfiguratorRegistry.Clear();
}
