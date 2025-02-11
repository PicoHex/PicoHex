namespace PicoHex.Logger.Abstractions;

public interface ILogSink : IDisposable, IAsyncDisposable
{
    void Write(string formattedMessage);
    ValueTask WriteAsync(string formattedMessage, CancellationToken cancellationToken = default);
}
