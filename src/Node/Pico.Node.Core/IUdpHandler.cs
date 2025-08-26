namespace Pico.Node.Core;

public interface IUdpHandler
{
    Task HandleAsync(
        ReadOnlyMemory<byte> datagram,
        IPEndPoint remoteEndPoint,
        CancellationToken ct
    );
}
