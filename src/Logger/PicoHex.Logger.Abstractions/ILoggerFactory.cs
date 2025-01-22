namespace PicoHex.Logger.Abstractions;

public interface ILoggerFactory : IAsyncDisposable
{
    ILogger CreateLogger(string categoryName);
    void AddProvider(ILoggerProvider provider);
}
