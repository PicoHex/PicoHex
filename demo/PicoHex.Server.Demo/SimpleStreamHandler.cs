namespace PicoHex.Server.Demo;

public class SimpleStreamHandler : ITcpHandler
{
    public async ValueTask HandleAsync(NetworkStream stream, CancellationToken cancellationToken)
    {
        var buffer = new byte[1024];

        try
        {
            // Read from the stream asynchronously
            int bytesRead;
            while (
                (
                    bytesRead = await stream.ReadAsync(
                        buffer.AsMemory(0, buffer.Length),
                        cancellationToken
                    )
                ) > 0
            )
            {
                var receivedData = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                Console.WriteLine($"Received: {receivedData}");

                // Send a response back
                var response = "Message received!";
                var responseBytes = Encoding.UTF8.GetBytes(response);
                await stream.WriteAsync(responseBytes, 0, responseBytes.Length, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error handling stream: {ex.Message}");
        }
    }
}
