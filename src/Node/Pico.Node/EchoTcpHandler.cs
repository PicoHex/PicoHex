using System.Text;

namespace Pico.Node;

public class EchoTcpHandler(ILogger<EchoTcpHandler> logger) : ITcpHandler
{
    public async Task HandleAsync(
        PipeReader reader,
        PipeWriter writer,
        IPEndPoint remoteEndPoint,
        CancellationToken ct
    )
    {
        try
        {
            reader.TryRead(out var read);

            await logger.InfoAsync(
                $"Received {read.Buffer.Length} bytes from {remoteEndPoint}",
                cancellationToken: ct
            );

            // In a real implementation, you would process the message here
            // and potentially send a response through a provided response mechanism

            var messageText = Encoding.UTF8.GetString(read.Buffer.First.Span);
            await logger.InfoAsync($"Message content: {messageText}", cancellationToken: ct);

            // For an echo server, we would need a way to send data back to the client
            // This would typically be handled by the TcpNode implementation
        }
        catch (Exception ex)
        {
            await logger.ErrorAsync(
                $"Error handling TCP message from {remoteEndPoint}",
                ex,
                cancellationToken: ct
            );
        }
    }
}
