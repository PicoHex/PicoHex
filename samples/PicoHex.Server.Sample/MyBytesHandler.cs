namespace PicoHex.Server.Sample;

public class MyBytesHandler(ILogger<MyBytesHandler> logger) : IUdpHandler
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
            await _logger.InfoAsync(
                $"Received datagram from {remoteEndPoint}",
                cancellationToken: cancellationToken
            );

            // Example of processing the data (e.g., echoing it back or processing it)
            if (data.Length > 0)
            {
                await _logger.InfoAsync(
                    $"Data received: {BitConverter.ToString(data)}",
                    cancellationToken: cancellationToken
                );
            }
            else
            {
                await _logger.WarningAsync(
                    "Received empty datagram.",
                    cancellationToken: cancellationToken
                );
            }

            // Simulate processing delay
            await Task.Delay(100, cancellationToken);
        }
        catch (Exception ex)
        {
            await _logger.ErrorAsync(
                "Error while handling datagram",
                ex,
                cancellationToken: cancellationToken
            );
        }
    }
}
