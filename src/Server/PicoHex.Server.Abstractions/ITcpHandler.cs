namespace PicoHex.Server.Abstractions;

public interface ITcpHandler
{
    ValueTask HandleAsync(NetworkStream stream, CancellationToken cancellationToken = default);
}
