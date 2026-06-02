namespace PicoDI;

public sealed partial class SvcScope
{
    /// <inheritdoc />
    public ISvcScope CreateScope()
    {
        DisposalGuards.ThrowIfDisposed(ref _disposed, nameof(SvcScope));

        var childScope = new SvcScope(_registrationCache);

        childScope.ParentScope = this;

        try
        {
            _childScopes.AddToHead(childScope, nameof(SvcScope));
        }
        catch (ObjectDisposedException)
        {
            // The parent was disposed between ThrowIfDisposed and AddToHead.
            // Dispose the orphaned child synchronously before rethrowing so it
            // does not leak. Using GetAwaiter().GetResult() (matching the
            // DisposeTrackedInstance pattern) ensures deterministic cleanup.
            try
            {
                childScope.DisposeAsync().AsTask().GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                OwningContainer?.OnError?.Invoke(ex, "Error disposing orphaned child scope");
            }
            throw;
        }

        return childScope;
    }

    private void DetachFromParent()
    {
        var parent = ParentScope;
        if (parent != null)
        {
            parent.DetachChildScope(this);
            return;
        }

        OwningContainer?.DetachRootScope(this);
    }

    private void DetachChildScope(SvcScope child)
    {
        if (!ReferenceEquals(child.ParentScope, this))
            return;

        _childScopes.Remove(child);
    }

    private List<SvcScope> DetachAllChildScopes()
    {
        return _childScopes.DrainAll();
    }
}
