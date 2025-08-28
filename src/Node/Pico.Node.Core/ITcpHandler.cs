namespace Pico.Node.Core;

public interface ITcpHandler
{
    Task HandleAsync(
        PipeReader reader,
        PipeWriter writer,
        IPEndPoint remoteEndPoint,
        CancellationToken ct
    );
}
