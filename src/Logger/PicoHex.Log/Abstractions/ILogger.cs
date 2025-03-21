namespace PicoHex.Log.Abstractions;

public interface ILogger
{
    IDisposable BeginScope<TState>(TState state);
    void Log(LogLevel logLevel, string message, Exception? exception = null);
}

public interface ILogger<out TCategory> : ILogger;
