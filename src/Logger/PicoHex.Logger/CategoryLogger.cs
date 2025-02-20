namespace PicoHex.Logger;

internal class CategoryLogger(string category, ILogSink sink) : ILogger
{
    private LogScope? _scope;

    public void Log(LogLevel level, string message, Exception? exception = null)
    {
        if (level < sink.MinimumLevel)
            return;

        var entry = GenerateEntry(level, message, exception);

        sink.Emit(entry);
    }

    public async ValueTask LogAsync(
        LogLevel level,
        string message,
        Exception? exception = null,
        CancellationToken cancellationToken = default
    )
    {
        if (level < sink.MinimumLevel)
            return;

        var entry = GenerateEntry(level, message, exception);

        await sink.EmitAsync(entry, cancellationToken);
    }

    private LogEntry GenerateEntry(LogLevel level, string message, Exception? exception = null) =>
        new(DateTime.Now, level, category, message, exception);

    public IDisposable BeginScope<TState>(TState state)
    {
        _scope ??= new LogScope(state);

        var scope = new LogScope(state, DisposeScope);
        var dict = state as IEnumerable<KeyValuePair<string, object>>;

        var scopeData =
            dict?.ToDictionary(kv => kv.Key, kv => kv.Value)
            ?? new Dictionary<string, object> { ["Scope"] = state! };

        (_scopes.Value ??= new Stack<Dictionary<string, object>>()).Push(scopeData);

        return scope;
    }

    private void DisposeScope(LoggerScope scope)
    {
        if (_scope.Value?.Count > 0)
        {
            _scope.Value.Pop();
        }
    }
}
