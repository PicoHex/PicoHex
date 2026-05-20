namespace PicoLog;

internal sealed class LoggerScopeProvider
{
    private readonly AsyncLocal<Scope?> _currentScope = new();

    public ILogScope Push(object state)
    {
        var scope = new Scope(this, state, _currentScope.Value);
        _currentScope.Value = scope;
        return scope;
    }

    public ScopeSnapshot Capture()
    {
        Scope? current = _currentScope.Value;

        if (current is null)
            return default;

        var scopes = new object[current.Depth];
        List<KeyValuePair<string, object?>>? properties = null;
        var index = scopes.Length;

        while (current is not null)
        {
            scopes[--index] = current.State;

            if (current.State is IEnumerable<KeyValuePair<string, object?>> kvPairs)
            {
                properties ??=  [];
                properties.AddRange(kvPairs);
            }

            current = current.Parent;
        }

        return new ScopeSnapshot(scopes, properties);
    }

    public static ILogScope Empty { get; } = new EmptyScope();

    private static Scope? FindNearestActiveAncestor(Scope? scope)
    {
        while (scope is not null)
        {
            if (!scope.IsDisposed)
                return scope;

            scope = scope.Parent;
        }

        return null;
    }

    private sealed class Scope(LoggerScopeProvider owner, object state, Scope? parent) : ILogScope
    {
        private int _disposed;

        public object State { get; } = state;

        public Scope? Parent { get; } = parent;

        public int Depth { get; } = (parent?.Depth ?? 0) + 1;

        public bool IsDisposed => Volatile.Read(ref _disposed) != 0;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0)
                return;

            if (ReferenceEquals(owner._currentScope.Value, this))
                owner._currentScope.Value = FindNearestActiveAncestor(Parent);
        }
    }

    private sealed class EmptyScope : ILogScope
    {
        public object State { get; } = string.Empty;

        public void Dispose() { }
    }
}

internal readonly record struct ScopeSnapshot(
    IReadOnlyList<object>? Scopes,
    IReadOnlyList<KeyValuePair<string, object?>>? Properties
);
