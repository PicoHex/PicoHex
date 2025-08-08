namespace Pico.Node.Abs;

public interface IUdpHandler
{
    ValueTask HandleAsync(
        ReadOnlyMemory<byte> data,
        EndPoint remoteEndPoint,
        CancellationToken cancellationToken = default
    );
}
