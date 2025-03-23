namespace PicoHex.Log;

public class LoggerFactory(ILogSink sink) : ILoggerFactory
{
    private readonly AsyncLocal<Stack<object>> _scopes = new();
    public LogLevel MinLevel { get; set; } = LogLevel.Debug;

    public ILogger CreateLogger(string categoryName) =>
        new InternalLogger(categoryName, sink, MinLevel, this);

    private class InternalLogger(
        string category,
        ILogSink sink,
        LogLevel minLevel,
        LoggerFactory factory
    ) : ILogger
    {
        public IDisposable BeginScope<TState>(TState state)
        {
            var stack = factory._scopes.Value ??= new Stack<object>();
            stack.Push(state!);

            return new Scope(() =>
            {
                if (stack.Count > 0)
                    stack.Pop();
            });
        }

        public void Log(LogLevel logLevel, string message, Exception? exception)
        {
            if (logLevel < minLevel)
                return;

            var entry = new LogEntry
            {
                Timestamp = DateTimeOffset.Now,
                Level = logLevel,
                Category = category,
                Message = message,
                Exception = exception,
                Scopes = factory._scopes.Value?.Reverse().ToList()
            };

            _ = sink.WriteAsync(entry); // Fire and forget
        }

        public async ValueTask LogAsync(
            LogLevel logLevel,
            string message,
            Exception? exception = null,
            CancellationToken cancellationToken = default
        )
        {
            if (logLevel < minLevel)
                return;

            var entry = new LogEntry
            {
                Timestamp = DateTimeOffset.Now,
                Level = logLevel,
                Category = category,
                Message = message,
                Exception = exception,
                Scopes = factory._scopes.Value?.Reverse().ToList()
            };

            await sink.WriteAsync(entry, cancellationToken);
        }

        private class Scope(Action onDispose) : IDisposable
        {
            public void Dispose() => onDispose?.Invoke();
        }
    }
}
