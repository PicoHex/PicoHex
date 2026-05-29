namespace PicoDI;

/// <summary>
/// Coordinates source-generated container configuration.
/// The global configurator registry is separate from the per-container generated-state gate.
/// </summary>
public static class SvcContainerAutoConfiguration
{
    private static readonly ConfiguratorRegistry Configurators = new();
    private static readonly GeneratedConfigurationStateRegistry ConfigurationStates = new();
    private static readonly Lock _applyLock = new();

    private sealed class ConfiguratorRegistry
    {
        private readonly Dictionary<string, Action<ISvcContainer>> _configurators =
            new(StringComparer.Ordinal);
        private Action<ISvcContainer>[]? _sortedSnapshot;

        public void Register(string configuratorId, Action<ISvcContainer> configurator)
        {
            lock (_configurators)
            {
                _configurators[configuratorId] = configurator;
                _sortedSnapshot = null;
            }
        }

        public Action<ISvcContainer>[] SnapshotInApplyOrder()
        {
            var snap = Volatile.Read(ref _sortedSnapshot);
            if (snap != null)
                return snap;

            lock (_configurators)
            {
                if (_configurators.Count is 0)
                    return [];

                var list = new List<KeyValuePair<string, Action<ISvcContainer>>>(_configurators);
                list.Sort(static (a, b) => string.CompareOrdinal(a.Key, b.Key));
                Action<ISvcContainer>[] sorted = new Action<ISvcContainer>[list.Count];
                for (int i = 0; i < list.Count; i++)
                    sorted[i] = list[i].Value;

                Volatile.Write(ref _sortedSnapshot, sorted);
                return sorted;
            }
        }

        public bool HasAny
        {
            get
            {
                lock (_configurators)
                {
                    return _configurators.Count > 0;
                }
            }
        }

        public void Clear()
        {
            lock (_configurators)
            {
                _configurators.Clear();
                _sortedSnapshot = null;
            }
        }
    }

    private sealed class GeneratedConfigurationStateRegistry
    {
        private sealed class ConfigurationState
        {
            public bool IsGeneratedConfigurationApplied;
        }

        private readonly ConditionalWeakTable<ISvcContainer, ConfigurationState> _states = new();

        public void MarkApplied(ISvcContainer container)
        {
            if (container is IGeneratedConfigurationStateContainer generatedStateContainer)
            {
                generatedStateContainer.IsGeneratedConfigurationApplied = true;
                return;
            }

            _states.GetOrCreateValue(container).IsGeneratedConfigurationApplied = true;
        }

        public bool HasApplied(ISvcContainer container)
        {
            if (container is IGeneratedConfigurationStateContainer generatedStateContainer)
            {
                return generatedStateContainer.IsGeneratedConfigurationApplied;
            }

            return _states.TryGetValue(container, out var state)
                && state.IsGeneratedConfigurationApplied;
        }
    }

    /// <summary>
    /// Registers a configurator action using a stable identifier so repeated module initializers
    /// replace the existing registration instead of appending a duplicate.
    /// Configurators are applied in deterministic <see cref="StringComparer.Ordinal"/> order of this identifier.
    /// </summary>
    /// <param name="configuratorId">A stable identifier for the configurator.</param>
    /// <param name="configurator">The configuration action that registers services with the container.</param>
    public static void RegisterConfigurator(
        string configuratorId,
        Action<ISvcContainer> configurator
    )
    {
        if (configuratorId is null)
            throw new ArgumentNullException(nameof(configuratorId));
        if (configurator is null)
            throw new ArgumentNullException(nameof(configurator));

        Configurators.Register(configuratorId, configurator);
    }

    /// <summary>
    /// Applies all registered configurations to the given container exactly once.
    /// If any configurator throws, the container is NOT marked as configured,
    /// allowing retries on subsequent containers.
    /// </summary>
    /// <param name="container">The container to configure.</param>
    /// <returns>True if any configurators were registered and applied; otherwise, false.</returns>
    public static bool TryApplyConfiguration(ISvcContainer container)
    {
        if (container is null)
            throw new ArgumentNullException(nameof(container));

        lock (_applyLock)
        {
            if (HasAppliedGeneratedConfiguration(container))
                return false;

            var configurators = Configurators.SnapshotInApplyOrder();
            if (configurators.Length is 0)
                return false;

            foreach (var configurator in configurators)
            {
                configurator(container);
            }

            MarkGeneratedConfigurationApplied(container);
            return true;
        }
    }

    /// <summary>
    /// Marks the per-container generated-registration state as applied.
    /// This does not inspect the registry or apply any configurators.
    /// </summary>
    /// <param name="container">The configured container.</param>
    public static void MarkGeneratedConfigurationApplied(ISvcContainer container)
    {
        if (container is null)
            throw new ArgumentNullException(nameof(container));

        ConfigurationStates.MarkApplied(container);
    }

    /// <summary>
    /// Gets a value indicating whether source-generated registrations were already applied
    /// to the given container instance.
    /// </summary>
    /// <param name="container">The container to inspect.</param>
    /// <returns>True if generated registrations have been applied to this container.</returns>
    public static bool HasAppliedGeneratedConfiguration(ISvcContainer container)
    {
        if (container is null)
            throw new ArgumentNullException(nameof(container));

        return ConfigurationStates.HasApplied(container);
    }

    /// <summary>
    /// Gets a value indicating whether any configurators have been registered.
    /// </summary>
    public static bool HasConfigurator
    {
        get => Configurators.HasAny;
    }

    /// <summary>
    /// Clears all registered configurators to enable isolated test execution.
    /// Only use in test scenarios — production code should never call this.
    /// </summary>
    internal static void ClearForTesting()
    {
        Configurators.Clear();
    }
}
