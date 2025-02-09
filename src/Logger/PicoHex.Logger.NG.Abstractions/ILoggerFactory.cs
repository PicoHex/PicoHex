namespace PicoHex.Logger.NG.Abstractions;

public interface ILoggerFactory
{
    ILogger CreateLogger(string category);
    ILogger<T> CreateLogger<T>();
    void AddProvider(ILoggerProvider provider);
}
