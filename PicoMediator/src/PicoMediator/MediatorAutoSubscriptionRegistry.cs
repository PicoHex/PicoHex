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

        // Bookkeeping under the lock; configurators run OUTSIDE it. Running
        // them under the lock deadlocks with the loader lock when a
        // configurator's first JIT of a cold assembly triggers that assembly's
        // module initializer (which calls Register): the JIT thread waits for
        // the loader lock while the initializer thread waits for ApplyLock
        // (2026-08-05 deadlock regression).
        Action<ISvcContainer>[] snapshot;
        lock (ApplyLock)
        {
            if (Applied.TryGetValue(container, out _))
                return false;

            snapshot = SortedSnapshot;
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

            // Mark applied BEFORE running the configurators: two concurrent
            // callers for the same container must not both run them.
            Applied.Add(container, Sentinel);
        }

        try
        {
            foreach (var configurator in snapshot)
                configurator(container);
            return true;
        }
        catch
        {
            // Roll back the marker so a later call can retry (documented
            // contract: "if any configurator throws, the container is NOT
            // marked as applied").
            lock (ApplyLock)
                Applied.Remove(container);
            throw;
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
