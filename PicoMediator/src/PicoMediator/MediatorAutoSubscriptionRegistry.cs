namespace PicoMediator;

/// <summary>
/// Coordinates source-generated handler registrations for PicoMediator.
/// Mirrors PicoDI.SvcContainerAutoConfiguration: generated code registers
/// configurators by stable id; AddPicoMediator() applies them to a container
/// exactly once. All public methods are lock-protected (module initializers
/// race with concurrent container creation).
/// </summary>
public static class MediatorAutoSubscriptionRegistry
{
    private static readonly object Sentinel = new();
    private static readonly Lock ApplyLock = new();
    private static readonly Dictionary<string, Action<ISvcContainer>> Configurators = new(
        StringComparer.Ordinal
    );
    private static Action<ISvcContainer>[]? SortedSnapshot;
    private static readonly ConditionalWeakTable<ISvcContainer, object> Applied = new();

    /// <summary>
    /// Registers a configurator using a stable identifier so repeated module
    /// initializers replace the existing registration instead of appending a
    /// duplicate. Configurators are applied in deterministic
    /// <see cref="StringComparer.Ordinal"/> order of this identifier.
    /// Intended for PicoMediator.Gen generated code.
    /// </summary>
    public static void Register(string configuratorId, Action<ISvcContainer> configurator)
    {
        ArgumentNullException.ThrowIfNull(configuratorId);
        ArgumentNullException.ThrowIfNull(configurator);

        lock (ApplyLock)
        {
            Configurators[configuratorId] = configurator;
            SortedSnapshot = null;
        }
    }

    /// <summary>
    /// Applies all registered configurators to the given container exactly
    /// once. If any configurator throws, the container is NOT marked as
    /// applied, allowing retries on subsequent calls.
    /// </summary>
    public static bool TryApplyConfiguration(ISvcContainer container)
    {
        ArgumentNullException.ThrowIfNull(container);

        lock (ApplyLock)
        {
            if (Applied.TryGetValue(container, out _))
                return false;

            var snapshot = SortedSnapshot;
            if (snapshot is null)
            {
                if (Configurators.Count is 0)
                    return false;

                var list = new List<KeyValuePair<string, Action<ISvcContainer>>>(Configurators);
                list.Sort(static (a, b) => string.CompareOrdinal(a.Key, b.Key));
                snapshot = new Action<ISvcContainer>[list.Count];
                for (var i = 0; i < list.Count; i++)
                    snapshot[i] = list[i].Value;
                SortedSnapshot = snapshot;
            }

            foreach (var configurator in snapshot)
                configurator(container);

            Applied.Add(container, Sentinel);
            return true;
        }
    }

    /// <summary>Clears all registered configurators to enable isolated test execution.</summary>
    internal static void ClearForTesting()
    {
        lock (ApplyLock)
        {
            Configurators.Clear();
            SortedSnapshot = null;
            Applied.Clear();
        }
    }
}
