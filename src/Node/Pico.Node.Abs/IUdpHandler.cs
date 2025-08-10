// Updated handler interface
namespace Pico.Node.Abs;

public interface IUdpHandler
{
    ValueTask HandleAsync(
        ReadOnlyMemory<byte> data,
        IPEndPoint remoteEndPoint, // More specific type
        CancellationToken cancellationToken = default
    );
}
