namespace PicoDI;

public sealed partial class SvcScope
{
    /// <inheritdoc />
    public ISvcScope CreateScope()
    {
        DisposalGuards.ThrowIfDisposed(ref _disposed, nameof(SvcScope));

        var childScope = new SvcScope(_registrationCache, _singletonCache);

        childScope.ParentScope = this;

        try
        {
            _childScopes.AddToHead(childScope, nameof(SvcScope));
        }
        catch (ObjectDisposedException)
        {
            // The parent was disposed between ThrowIfDisposed and AddToHead.
            // Dispose the orphaned child before rethrowing so it does not leak.
            childScope
                .DisposeAsync()
                .AsTask()
                .ContinueWith(
                    static t =>
                        Trace.WriteLine($"Error disposing orphaned child scope: {t.Exception}"),
                    CancellationToken.None,
                    TaskContinuationOptions.OnlyOnFaulted,
                    TaskScheduler.Default
                );
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
