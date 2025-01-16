namespace PicoHex.Server.Abstractions;

public interface IUdpHandler
{
    ValueTask HandleAsync(
        byte[] data,
        EndPoint remoteEndPoint,
        CancellationToken cancellationToken = default
    );
}
