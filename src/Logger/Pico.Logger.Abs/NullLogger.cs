namespace Pico.Logger.Abs;

public class NullLogger : ILogger
{
    public IDisposable BeginScope<TState>(TState state)
        where TState : notnull
    {
        return NullScope.Instance;
    }

    public void Log(LogLevel logLevel, string message, Exception? exception = null) { }

    public ValueTask LogAsync(
        LogLevel logLevel,
        string message,
        Exception? exception = null,
        CancellationToken cancellationToken = default
    )
    {
        return ValueTask.CompletedTask;
    }
}

public class NullLogger<TCategory> : ILogger<TCategory>
{
    public IDisposable BeginScope<TState>(TState state)
        where TState : notnull
    {
        return NullScope.Instance;
    }

    public void Log(LogLevel logLevel, string message, Exception? exception = null) { }

    public ValueTask LogAsync(
        LogLevel logLevel,
        string message,
        Exception? exception = null,
        CancellationToken cancellationToken = default
    )
    {
        return ValueTask.CompletedTask;
    }
}

public class NullScope : IDisposable
{
    public static NullScope Instance { get; } = new();

    private NullScope() { }

    public void Dispose() { }
}
