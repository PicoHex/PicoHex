namespace PicoHex.Logger.Console;

public class LoggerProvider(ILogSink sink) : ILoggerProvider
{
    public ILogger CreateLogger(string category)
    {
        return new CategoryLogger(category, sink);
    }

    public void Dispose() { }

    private class CategoryLogger(string category, ILogSink sink) : ILogger
    {
        public void Log(LogLevel level, string message, Exception? exception = null)
        {
            if (level < sink.MinimumLevel)
                return;

            var entry = new LogEntry
            {
                Timestamp = DateTime.Now,
                Level = level,
                Category = category,
                Message = message,
                Exception = exception
            };

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

            var entry = new LogEntry
            {
                Timestamp = DateTime.Now,
                Level = level,
                Category = category,
                Message = message,
                Exception = exception
            };

            await sink.EmitAsync(entry, cancellationToken);
        }

        public IDisposable BeginScope<TState>(TState state)
        {
            throw new NotImplementedException();
        }
    }
}
