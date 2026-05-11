namespace PicoDI;

/// <summary>
/// Represents a service scope that manages the lifetime of scoped service instances.
/// Supports hierarchical scopes where child scopes are automatically disposed when the parent is disposed.
/// </summary>
public sealed partial class SvcScope : ISvcScope
{
    private readonly FrozenDictionary<Type, SvcRuntimeRegistration[]> _registrationCache;

    /// <summary>
    /// Fast singleton cache: direct Type -> runtime registration lookup for singleton services.
    /// Avoids array indexing overhead in the hot path.
    /// </summary>
    private readonly FrozenDictionary<Type, SvcRuntimeRegistration> _singletonCache;

    // Scoped instances: ConcurrentDictionary with Lazy<T>(ExecutionAndPublication).
    // The Lazy wrapper ensures the factory is called at most once, even under
    // contention where ConcurrentDictionary.GetOrAdd may invoke the valueFactory
    // multiple times. Discarded Lazy wrappers are lightweight and never execute.
    private ConcurrentDictionary<SvcRuntimeRegistration, Lazy<object>>? _scopedInstances;

    // Tracks creation order of scoped instances within this scope.
    // Used by the disposal pass to release instances in reverse construction
    // order (LIFO), so a scoped service can safely use its dependencies in
    // its own Dispose method.
    private ConcurrentQueue<(long Order, object Instance)>? _scopedCreationOrder;

    // Transient disposable instances: ConcurrentQueue for lock-free enqueue on the hot path.
    // Lazily initialized — most scopes never resolve transient IDisposable/IAsyncDisposable services.
    private ConcurrentQueue<object>? _trackedTransients;

    private readonly TrackedScopeList _childScopes = new();

    internal SvcContainer? OwningContainer;
    internal SvcScope? ParentScope;
    internal SvcScope? PreviousTrackedScope;
    internal SvcScope? NextTrackedScope;

    private int _disposed; // 0 = not disposed, 1 = disposed (for thread-safe Interlocked operations)

    internal SvcScope(
        FrozenDictionary<Type, SvcRuntimeRegistration[]> registrationCache,
        FrozenDictionary<Type, SvcRuntimeRegistration> singletonCache
    )
    {
        _registrationCache = registrationCache;
        _singletonCache = singletonCache;
    }
}
