namespace Pico.Node;

public class EchoUdpHandler(ILogger<EchoUdpHandler> logger, UdpNode udpNode) : IUdpHandler
{
    public async Task HandleAsync(
        ReadOnlyMemory<byte> datagram,
        IPEndPoint remoteEndPoint,
        CancellationToken ct
    )
    {
        try
        {
            await logger.InfoAsync(
                $"Received {datagram.Length} bytes from {remoteEndPoint}",
                cancellationToken: ct
            );

            // Echo the message back to the sender
            await udpNode.SendAsync(datagram, remoteEndPoint, ct);

            await logger.InfoAsync(
                "Echoed {datagram.Length} bytes back to {remoteEndPoint}",
                cancellationToken: ct
            );
        }
        catch (Exception ex)
        {
            await logger.ErrorAsync(
                "Error handling UDP message from {remoteEndPoint}",
                ex,
                cancellationToken: ct
            );
        }
    }
}
