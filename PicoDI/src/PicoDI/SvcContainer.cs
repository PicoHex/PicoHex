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
    /// Callback invoked when an exception is caught during disposal or error-recovery paths.
    /// Set this to observe errors that would otherwise be silently swallowed (default: <see langword="null"/>).
    /// </summary>
    /// <remarks>
    /// The <c>string</c> parameter provides human-readable context for the failure.
    /// The callback must not throw; exceptions from the callback itself are silently discarded.
    /// </remarks>
    public Action<Exception, string>? OnError { get; set; }

    /// <summary>
    /// Frozen (optimized) runtime registration cache after Build() is called.
    /// </summary>
    private FrozenDictionary<Type, SvcRuntimeRegistration[]>? _frozenCache;

    /// <summary>
    /// Fast singleton cache: Type -> runtime registration (for singleton services only).
    /// </summary>
    private FrozenDictionary<Type, SvcRuntimeRegistration>? _singletonCache;

    bool IGeneratedConfigurationStateContainer.IsGeneratedConfigurationApplied { get; set; }

    /// <summary>
    /// Interceptor filters registered via AddInterceptor&lt;T&gt;().Where*().
    /// Source generators evaluate filters at compile time. Runtime evaluation exists
    /// as a fallback for non-generator scenarios.
    /// </summary>
    internal readonly List<IInterceptorFilter> InterceptorFilters = [];

    /// <summary>
    /// Service-level interceptor exclusions registered via WithoutInterceptor&lt;T&gt;().
    /// </summary>
    internal readonly List<IInterceptorExclusion> InterceptorExclusions = [];

    private int _disposed;

    /// <inheritdoc />
    public bool IsRegistered(Type serviceType)
    {
        ArgumentNullException.ThrowIfNull(serviceType);

        var frozen = Volatile.Read(ref _frozenCache);
        if (frozen is not null)
            return frozen.ContainsKey(serviceType);

        var cache = Volatile.Read(ref _registrationCache);
        return cache is not null && cache.ContainsKey(serviceType);
    }

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
