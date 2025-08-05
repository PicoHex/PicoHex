namespace Pico.Node.Abs;

public interface ITcpHandler
{
    ValueTask HandleAsync(NetworkStream stream, CancellationToken cancellationToken = default);
}
