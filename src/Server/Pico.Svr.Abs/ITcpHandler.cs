namespace Pico.SVR.Abs;

public interface ITcpHandler
{
    ValueTask HandleAsync(NetworkStream stream, CancellationToken cancellationToken = default);
}
