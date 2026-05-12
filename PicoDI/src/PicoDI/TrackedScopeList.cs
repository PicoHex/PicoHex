namespace PicoDI;

/// <summary>
/// Internal doubly-linked list tracker for <see cref="SvcScope"/> instances.
/// Used by both <see cref="SvcContainer"/> (root scopes) and <see cref="SvcScope"/> (child scopes)
/// to eliminate duplicated linked-list manipulation code.
/// </summary>
internal sealed class TrackedScopeList
{
    private SvcScope? _first;
    private readonly Lock _lock = new();
    private bool _sealed;

    /// <summary>
    /// Inserts <paramref name="scope"/> at the head of the list.
    /// Caller is responsible for setting <see cref="SvcScope.OwningContainer"/> or
    /// <see cref="SvcScope.ParentScope"/> before calling this method.
    /// </summary>
    /// <param name="containerName">The name of the containing type (SvcContainer or SvcScope).</param>
    public void AddToHead(SvcScope scope, string containerName = nameof(SvcScope))
    {
        lock (_lock)
        {
            if (_sealed)
                throw new ObjectDisposedException(containerName);

            scope.NextTrackedScope = _first;
            if (_first != null)
                _first.PreviousTrackedScope = scope;

            _first = scope;
        }
    }

    /// <summary>
    /// Unlinks <paramref name="scope"/> from the list and clears all its tracking pointers
    /// (<see cref="SvcScope.ParentScope"/>, <see cref="SvcScope.OwningContainer"/>,
    /// <see cref="SvcScope.PreviousTrackedScope"/>, <see cref="SvcScope.NextTrackedScope"/>).
    /// Caller should verify the scope is parented/owned by the expected container/scope before calling.
    /// </summary>
    public void Remove(SvcScope scope)
    {
        lock (_lock)
        {
            // Caller guarantees that scope is parented to the expected container/scope.
            // See DetachChildScope and DetachRootScope for the ReferenceEquals guard.
            if (scope.ParentScope is null && scope.OwningContainer is null)
                throw new InvalidOperationException(
                    "Remove called on scope that is not parented to any container/scope."
                );

            if (!_sealed)
            {
                var prev = scope.PreviousTrackedScope;
                var next = scope.NextTrackedScope;

                if (prev != null)
                    prev.NextTrackedScope = next;
                else if (ReferenceEquals(_first, scope))
                    _first = next;
                else
                    return; // Not in this list — don't corrupt tracking pointers

                if (next != null)
                    next.PreviousTrackedScope = prev;
            }

            scope.ParentScope = null;
            scope.OwningContainer = null;
            scope.PreviousTrackedScope = null;
            scope.NextTrackedScope = null;
        }
    }

    /// <summary>
    /// Seals the list and drains all tracked scopes, clearing their tracking pointers.
    /// After this call, <see cref="AddToHead"/> will throw.
    /// Returns an empty list if no scopes were tracked.
    /// </summary>
    public List<SvcScope> DrainAll()
    {
        lock (_lock)
        {
            _sealed = true;

            var current = _first;
            _first = null;
            if (current is null)
                return [];

            var scopes = new List<SvcScope>();
            while (current != null)
            {
                var next = current.NextTrackedScope;
                current.OwningContainer = null;
                current.ParentScope = null;
                current.PreviousTrackedScope = null;
                current.NextTrackedScope = null;
                scopes.Add(current);
                current = next;
            }

            return scopes;
        }
    }
}
