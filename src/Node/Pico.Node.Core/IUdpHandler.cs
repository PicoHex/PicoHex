namespace Pico.Node.Core;

public interface IUdpHandler
{
    ValueTask HandleAsync(
        ReadOnlyMemory<byte> datagram,
        IPEndPoint remoteEndPoint,
        CancellationToken cancellationToken = default
    );
}
