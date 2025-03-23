namespace PicoHex.Log;

public class LoggerFactory(IEnumerable<ILogSink> sinks) : ILoggerFactory
{
    private readonly AsyncLocal<Stack<object>> _scopes = new();
    public LogLevel MinLevel { get; set; } = LogLevel.Debug;

    public ILogger CreateLogger(string categoryName) =>
        new InternalLogger(categoryName, sinks, this);

    private class InternalLogger : ILogger, IDisposable
    {
        private readonly string _categoryName;
        private readonly Channel<LogEntry> _channel = Channel.CreateBounded<LogEntry>(
            new BoundedChannelOptions(1000) { FullMode = BoundedChannelFullMode.DropOldest }
        );
        private readonly Task _processingTask;
        private readonly IEnumerable<ILogSink> _sinks;
        private readonly LoggerFactory _factory;
        private readonly ILogSink _defaultSink;

        public InternalLogger(
            string categoryName,
            IEnumerable<ILogSink> sinks,
            LoggerFactory factory
        )
        {
            _sinks = sinks;
            _factory = factory;
            _categoryName = categoryName;
            _processingTask = Task.Run(ProcessEntries);
            _defaultSink = _sinks.First(p => p is ConsoleLogSink);
        }

        private async Task ProcessEntries()
        {
            await foreach (var entry in _channel.Reader.ReadAllAsync())
            {
                foreach (var sink in _sinks)
                {
                    try
                    {
                        await sink.WriteAsync(entry);
                    }
                    catch (Exception ex)
                    {
                        var sinkEntry = new LogEntry
                        {
                            Timestamp = DateTimeOffset.Now,
                            Level = LogLevel.Error,
                            Category = _categoryName,
                            Exception = ex
                        };
                        await _defaultSink.WriteAsync(sinkEntry);
                    }
                }
            }
        }

        public IDisposable BeginScope<TState>(TState state)
        {
            var stack = _factory._scopes.Value ??= new Stack<object>();
            stack.Push(state!);

            return new Scope(() =>
            {
                if (stack.Count > 0)
                    stack.Pop();
            });
        }

        public void Log(LogLevel logLevel, string message, Exception? exception)
        {
            if (logLevel < _factory.MinLevel)
                return;

            var entry = new LogEntry
            {
                Timestamp = DateTimeOffset.Now,
                Level = logLevel,
                Category = _categoryName,
                Message = message,
                Exception = exception,
                Scopes = _factory._scopes.Value?.Reverse().ToList()
            };

            _channel.Writer.TryWrite(entry); // Fire and forget
        }

        public async ValueTask LogAsync(
            LogLevel logLevel,
            string message,
            Exception? exception = null,
            CancellationToken cancellationToken = default
        )
        {
            if (logLevel < _factory.MinLevel)
                return;

            var entry = new LogEntry
            {
                Timestamp = DateTimeOffset.Now,
                Level = logLevel,
                Category = _categoryName,
                Message = message,
                Exception = exception,
                Scopes = _factory._scopes.Value?.Reverse().ToList()
            };

            await _channel.Writer.WriteAsync(entry, cancellationToken);
        }

        private class Scope(Action onDispose) : IDisposable
        {
            public void Dispose() => onDispose();
        }

        public void Dispose()
        {
            _channel.Writer.Complete();
            _processingTask.Wait();
            _processingTask.Dispose();
            foreach (var sink in _sinks)
                sink.Dispose();
        }
    }
}
