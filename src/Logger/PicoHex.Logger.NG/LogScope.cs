namespace PicoHex.Logger.NG;

internal class LogScope : ILogScope
{
    private readonly AsyncLocal<Stack<ILogScope>> _scopes = new();

    public object? State { get; }
    public IReadOnlyList<ILogScope> ActiveScopes => _scopes.Value?.Reverse().ToList() ?? [];

    public LogScope(object? state)
    {
        State = state;
        _scopes.Value ??= new Stack<ILogScope>();
        _scopes.Value.Push(this);
    }

    public void Dispose()
    {
        if (_scopes.Value?.Count > 0 && _scopes.Value.Peek() == this)
        {
            _scopes.Value.Pop();
        }
    }
}
