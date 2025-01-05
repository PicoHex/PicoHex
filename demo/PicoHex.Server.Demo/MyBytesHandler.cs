namespace PicoHex.Server.Demo;

public class MyBytesHandler(ILogger<MyBytesHandler> logger) : IBytesHandler
{
    private readonly ILogger<MyBytesHandler> _logger =
        logger ?? throw new ArgumentNullException(nameof(logger));

    // If logger is null, fallback to default console logger

    public async ValueTask HandleAsync(
        byte[] data,
        EndPoint remoteEndPoint,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            _logger.LogInformation($"Received datagram from {remoteEndPoint}");

            // Example of processing the data (e.g., echoing it back or processing it)
            if (data.Length > 0)
            {
                _logger.LogInformation($"Data received: {BitConverter.ToString(data)}");
            }
            else
            {
                _logger.LogWarning("Received empty datagram.");
            }

            // Simulate processing delay
            await Task.Delay(100, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while handling datagram");
        }
    }
}
