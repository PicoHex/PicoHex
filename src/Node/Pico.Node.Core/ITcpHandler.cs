namespace Pico.Node.Core;

public interface ITcpHandler
{
    ValueTask HandleAsync(NetworkStream stream, CancellationToken cancellationToken = default);
}
