namespace Pico.Node.Core;

public interface ITcpHandler
{
    ValueTask HandleAsync(
        PipeReader reader,
        PipeWriter writer,
        IPEndPoint remoteEndPoint,
        CancellationToken cancellationToken = default
    );
}
