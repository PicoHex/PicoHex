namespace PicoHex.Logger.NG;

public interface ILogger
{
    void Log(LogLevel level, string message, Exception? exception = null);
}

public interface ILogger<out T> : ILogger;

public interface ILogFormatter
{
    string Format(LogEntry entry);
}

public interface ILogSink
{
    LogLevel MinimumLevel { get; set; }
    void Emit(LogEntry entry);
}

public interface ILoggerProvider : IDisposable
{
    ILogger CreateLogger(string category);
}

public interface ILoggerFactory
{
    ILogger CreateLogger(string category);
    ILogger<T> CreateLogger<T>();
    void AddProvider(ILoggerProvider provider);
}
