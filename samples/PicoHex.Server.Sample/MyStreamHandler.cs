namespace PicoHex.Server.Sample;

public class MyStreamHandler(ILogger<MyStreamHandler> logger) : ITcpHandler
{
    private readonly ILogger<MyStreamHandler> _logger =
        logger ?? throw new ArgumentNullException(nameof(logger));

    // If logger is null, fallback to default console logger

    public async ValueTask HandleAsync(
        NetworkStream stream,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            // Example of logging inside the stream handler
            await _logger.InfoAsync(
                "Handling incoming stream...",
                cancellationToken: cancellationToken
            );

            // Simulate processing the stream (e.g., reading data)
            byte[] buffer = new byte[1024];
            int bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length, cancellationToken);

            if (bytesRead > 0)
            {
                await _logger.InfoAsync(
                    $"Received {bytesRead} bytes.",
                    cancellationToken: cancellationToken
                );
            }
            else
            {
                await _logger.WarningAsync(
                    "Received zero bytes or client closed the connection.",
                    cancellationToken: cancellationToken
                );
            }
        }
        catch (Exception ex)
        {
            await _logger.ErrorAsync(
                "Error while handling stream",
                ex,
                cancellationToken: cancellationToken
            );
        }
    }
}
