namespace PicoDI;

public sealed partial class SvcContainer
{
    /// <inheritdoc />
    public ISvcScope CreateScope()
    {
        DisposalGuards.ThrowIfDisposed(ref _disposed, nameof(SvcContainer));

        var frozenCache = Volatile.Read(ref _frozenCache);
        if (frozenCache is null)
        {
            Build();
            frozenCache = Volatile.Read(ref _frozenCache)!;
        }

        var scope = new SvcScope(
            frozenCache,
            Volatile.Read(ref _singletonCache)
                ?? FrozenDictionary<Type, SvcRuntimeRegistration>.Empty
        );

        scope.OwningContainer = this;

        try
        {
            _rootScopes.AddToHead(scope, nameof(SvcContainer));
        }
        catch (ObjectDisposedException)
        {
            // The container was disposed between ThrowIfDisposed and AddToHead.
            // Dispose the orphaned scope synchronously before rethrowing so it
            // does not leak. Using GetAwaiter().GetResult() (matching the
            // DisposeTrackedInstance pattern) ensures deterministic cleanup.
            try
            {
                scope.DisposeAsync().AsTask().GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"Error disposing orphaned scope: {ex}");
            }
            throw;
        }

        return scope;
    }

    internal void DetachRootScope(SvcScope scope)
    {
        if (!ReferenceEquals(scope.OwningContainer, this))
            return;

        _rootScopes.Remove(scope);
    }

    private List<SvcScope> DetachAllRootScopes()
    {
        return _rootScopes.DrainAll();
    }
}
