namespace PicoHex.Server;

public interface IBytesHandler
{
    ValueTask HandleAsync(
        byte[] data,
        EndPoint remoteEndPoint,
        CancellationToken cancellationToken = default
    );
}
