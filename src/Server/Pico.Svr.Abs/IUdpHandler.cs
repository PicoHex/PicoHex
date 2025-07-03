namespace Pico.SVR.Abs;

public interface IUdpHandler
{
    ValueTask HandleAsync(
        byte[] data,
        EndPoint remoteEndPoint,
        CancellationToken cancellationToken = default
    );
}
