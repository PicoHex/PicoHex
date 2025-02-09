namespace PicoHex.Logger.NG.Abstractions;

public interface ILoggerProvider : IDisposable
{
    ILogger CreateLogger(string category);
}
