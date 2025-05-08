namespace Pico.Svr.Sample;

public class MyBytesHandler(ILogger<MyBytesHandler> logger) : IUdpHandler
{
    // If logger is null, fallback to default console logger

    public async ValueTask HandleAsync(
        byte[] data,
        EndPoint remoteEndPoint,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            await logger.InfoAsync(
                $"Received datagram from {remoteEndPoint}",
                cancellationToken: cancellationToken
            );

            // Example of processing the data (e.g., echoing it back or processing it)
            if (data.Length > 0)
            {
                await logger.InfoAsync(
                    $"Data received: {BitConverter.ToString(data)}",
                    cancellationToken: cancellationToken
                );
            }
            else
            {
                await logger.WarningAsync(
                    "Received empty datagram.",
                    cancellationToken: cancellationToken
                );
            }

            // Simulate processing delay
            await Task.Delay(100, cancellationToken);
        }
        catch (Exception ex)
        {
            await logger.ErrorAsync(
                "Error while handling datagram",
                ex,
                cancellationToken: cancellationToken
            );
        }
    }
}
