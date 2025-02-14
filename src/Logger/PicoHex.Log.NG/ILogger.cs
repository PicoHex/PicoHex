namespace PicoHex.Log.NG;

public interface ILogger
{
    IDisposable BeginScope<TState>(TState state);
    void Log(LogLevel logLevel, string message, Exception? exception = null);
}

public interface ILogger<out TCategory> : ILogger { }
