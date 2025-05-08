namespace Pico.Svr.Abs;

public interface ITcpHandler
{
    ValueTask HandleAsync(NetworkStream stream, CancellationToken cancellationToken = default);
}
