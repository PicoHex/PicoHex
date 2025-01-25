namespace PicoHex.Logger.Abstractions;

public interface ILogSink : IDisposable, IAsyncDisposable
{
    ValueTask WriteAsync(string formattedMessage);
}
