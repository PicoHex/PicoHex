namespace PicoHex.Logger.Abstractions;

public interface ILogSink : IAsyncDisposable
{
    ValueTask WriteAsync(string formattedMessage);
}
