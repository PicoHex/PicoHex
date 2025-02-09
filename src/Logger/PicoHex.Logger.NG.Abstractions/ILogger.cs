namespace PicoHex.Logger.NG.Abstractions;

public interface ILogger
{
    void Log(LogLevel level, string message, Exception? exception = null);
    ValueTask LogAsync(
        LogLevel level,
        string message,
        Exception? exception = null,
        CancellationToken cancellationToken = default
    );
}

public interface ILogger<out T> : ILogger;
