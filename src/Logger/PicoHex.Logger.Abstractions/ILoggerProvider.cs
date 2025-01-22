namespace PicoHex.Logger.Abstractions;

public interface ILoggerProvider
{
    ILogger CreateLogger(string categoryName);
}
