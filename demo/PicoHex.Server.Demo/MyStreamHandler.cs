namespace PicoHex.Server.Demo;

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
            _logger.LogInformation("Handling incoming stream...");

            // Simulate processing the stream (e.g., reading data)
            byte[] buffer = new byte[1024];
            int bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length, cancellationToken);

            if (bytesRead > 0)
            {
                _logger.LogInformation($"Received {bytesRead} bytes.");
            }
            else
            {
                _logger.LogWarning("Received zero bytes or client closed the connection.");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while handling stream");
        }
    }
}
