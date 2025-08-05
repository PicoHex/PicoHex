namespace Pico.Node.Sample;

public class MyStreamHandler(ILogger<MyStreamHandler> logger) : ITcpHandler
{
    // If logger is null, fallback to default console logger

    public async ValueTask HandleAsync(
        NetworkStream stream,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            // Example of logging inside the stream handler
            await logger.InfoAsync(
                "Handling incoming stream...",
                cancellationToken: cancellationToken
            );

            // Simulate processing the stream (e.g., reading data)
            var buffer = new byte[1024];
            var bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length, cancellationToken);

            if (bytesRead > 0)
            {
                await logger.InfoAsync(
                    $"Received {bytesRead} bytes.",
                    cancellationToken: cancellationToken
                );
            }
            else
            {
                await logger.WarningAsync(
                    "Received zero bytes or client closed the connection.",
                    cancellationToken: cancellationToken
                );
            }
        }
        catch (Exception ex)
        {
            await logger.ErrorAsync(
                "Error while handling stream",
                ex,
                cancellationToken: cancellationToken
            );
        }
    }
}
