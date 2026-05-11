namespace PicoDI;

/// <summary>
/// A high-performance, AOT-compatible dependency injection container.
/// Manages service registrations, scope creation, and singleton instance lifecycle.
/// </summary>
/// <remarks>
/// Use <c>DisposeAsync()</c> for proper asynchronous cleanup of hosted services and scopes.
/// </remarks>
public sealed partial class SvcContainer : ISvcContainer, IGeneratedConfigurationStateContainer
{
    private Dictionary<Type, List<SvcRuntimeRegistration>>? _registrationCache;

    private readonly Lock _registrationLock = new();
    private readonly TrackedScopeList _rootScopes = new();

    /// <summary>
    /// Frozen (optimized) runtime registration cache after Build() is called.
    /// </summary>
    private FrozenDictionary<Type, SvcRuntimeRegistration[]>? _frozenCache;

    /// <summary>
    /// Fast singleton cache: Type -> runtime registration (for singleton services only).
    /// </summary>
    private FrozenDictionary<Type, SvcRuntimeRegistration>? _singletonCache;

    bool IGeneratedConfigurationStateContainer.IsGeneratedConfigurationApplied { get; set; }

    private int _disposed;

    /// <summary>
    /// Creates a new instance of <see cref="SvcContainer"/>.
    /// </summary>
    public SvcContainer(bool autoConfigureFromGenerator = true)
    {
        Volatile.Write(
            ref _registrationCache,
            new Dictionary<Type, List<SvcRuntimeRegistration>>()
        );
        if (autoConfigureFromGenerator)
        {
            SvcContainerAutoConfiguration.TryApplyConfiguration(this);
        }
    }
}
