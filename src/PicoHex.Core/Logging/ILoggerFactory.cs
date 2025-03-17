namespace PicoHex.Core.Logging;

public interface ILoggerFactory
{
    ILogger CreateLogger(string category);

    void AddProvider(ILoggerProvider provider);
}
