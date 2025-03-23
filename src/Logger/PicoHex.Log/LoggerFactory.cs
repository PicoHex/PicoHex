﻿namespace PicoHex.Log;

public class LoggerFactory(IEnumerable<ILogSink> sinks) : ILoggerFactory
{
    private readonly AsyncLocal<Stack<object>> _scopes = new();
    public LogLevel MinLevel { get; set; } = LogLevel.Debug;

    public ILogger CreateLogger(string categoryName) =>
        new InternalLogger(categoryName, sinks, MinLevel, this);

    private class InternalLogger : ILogger, IDisposable
    {
        private readonly Channel<LogEntry> _channel = Channel.CreateUnbounded<LogEntry>();
        private readonly Task _processingTask;
        private readonly string _category;
        private readonly IEnumerable<ILogSink> _sinks;
        private readonly LogLevel _minLevel;
        private readonly LoggerFactory _factory;

        public InternalLogger(
            string category,
            IEnumerable<ILogSink> sinks,
            LogLevel minLevel,
            LoggerFactory factory
        )
        {
            _category = category;
            _sinks = sinks;
            _minLevel = minLevel;
            _factory = factory;
            _processingTask = Task.Run(ProcessEntries);
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
                    catch
                    { /* 确保主线程不受影响 */
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
            if (logLevel < _minLevel)
                return;

            var entry = new LogEntry
            {
                Timestamp = DateTimeOffset.Now,
                Level = logLevel,
                Category = _category,
                Message = message,
                Exception = exception,
                Scopes = _factory._scopes.Value?.Reverse().ToList()
            };

            _channel.Writer.WriteAsync(entry); // Fire and forget
        }

        public async ValueTask LogAsync(
            LogLevel logLevel,
            string message,
            Exception? exception = null,
            CancellationToken cancellationToken = default
        )
        {
            if (logLevel < _minLevel)
                return;

            var entry = new LogEntry
            {
                Timestamp = DateTimeOffset.Now,
                Level = logLevel,
                Category = _category,
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
            _processingTask.Dispose();
            foreach (var sink in _sinks)
                sink.Dispose();
        }
    }
}
