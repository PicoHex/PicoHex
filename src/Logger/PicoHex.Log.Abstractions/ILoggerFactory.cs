namespace PicoHex.Log.Abstractions;

public interface ILoggerFactory
{
    ILogger CreateLogger(string categoryName);
}
