namespace PicoHex.Core.Logging;

public interface ILoggerProvider : IDisposable
{
    ILogger CreateLogger(string category);
}
