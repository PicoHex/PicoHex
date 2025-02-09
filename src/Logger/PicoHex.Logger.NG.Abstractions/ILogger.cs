namespace PicoHex.Logger.NG.Abstractions;

public interface ILogger
{
    void Log(LogLevel level, string message, Exception? exception = null);
}

public interface ILogger<out T> : ILogger;
