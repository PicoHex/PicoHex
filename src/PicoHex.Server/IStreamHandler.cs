namespace PicoHex.Server;

public interface IStreamHandler
{
    ValueTask HandleAsync(NetworkStream stream, CancellationToken cancellationToken = default);
}
