namespace PicoHex.Logger;

public class GenericTypeLogger<T>(ILoggerFactory factory) : ILogger<T>
{
    private readonly ILogger _logger = factory.CreateLogger(typeof(T).FullName!);
    private readonly AsyncLocal<Stack<object>> _scopes = new();

    public void Log(LogLevel level, string message, Exception? exception = null)
    {
        _logger.Log(level, message, exception);
    }

    public async ValueTask LogAsync(
        LogLevel level,
        string message,
        Exception? exception = null,
        CancellationToken cancellationToken = default
    )
    {
        await _logger.LogAsync(level, message, exception, cancellationToken);
    }

    public IDisposable BeginScope<TState>(TState state)
    {
        var stack = _scopes.Value ??= new Stack<object>();
        stack.Push(state!);

        return new LogScope(() =>
        {
            if (stack.Count > 0)
                stack.Pop();
        });
    }
}
