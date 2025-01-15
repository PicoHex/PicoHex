namespace PicoHex.Server.Http;

public class HttpHandler(ILogger<HttpHandler> logger) : IStreamHandler
{
    private readonly ILogger<HttpHandler> _logger =
        logger ?? throw new ArgumentNullException(nameof(logger));

    public async ValueTask HandleAsync(
        NetworkStream stream,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            // Read the request
            var request = await ReadRequestAsync(stream, cancellationToken);

            // Process the request (basic example: respond with a fixed response)
            var response = ProcessRequest(request);

            // Write the response
            await WriteResponseAsync(stream, response, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling HTTP request");

            // Attempt to write an error response to the client
            try
            {
                var errorResponse =
                    "HTTP/1.1 500 Internal Server Error\r\nContent-Length: 0\r\n\r\n";
                await WriteResponseAsync(stream, errorResponse, cancellationToken);
            }
            catch
            {
                // Suppress any exceptions while writing error response
            }
        }
    }

    private async Task<string> ReadRequestAsync(
        NetworkStream stream,
        CancellationToken cancellationToken
    )
    {
        using var reader = new StreamReader(stream, Encoding.UTF8, leaveOpen: true);

        var requestBuilder = new StringBuilder();
        char[] buffer = new char[1024];

        int bytesRead;
        while ((bytesRead = await reader.ReadAsync(buffer, 0, buffer.Length)) > 0)
        {
            requestBuilder.Append(buffer, 0, bytesRead);

            // Check if the end of the HTTP request headers has been reached
            if (requestBuilder.ToString().Contains("\r\n\r\n"))
            {
                break;
            }

            if (cancellationToken.IsCancellationRequested)
            {
                cancellationToken.ThrowIfCancellationRequested();
            }
        }

        return requestBuilder.ToString();
    }

    private string ProcessRequest(string request)
    {
        // Basic example: Always return a 200 OK response
        const string responseBody = "<html><body><h1>Hello, World!</h1></body></html>";
        return $"HTTP/1.1 200 OK\r\nContent-Type: text/html\r\nContent-Length: {responseBody.Length}\r\n\r\n{responseBody}";
    }

    private async Task WriteResponseAsync(
        NetworkStream stream,
        string response,
        CancellationToken cancellationToken
    )
    {
        byte[] responseBytes = Encoding.UTF8.GetBytes(response);
        await stream.WriteAsync(responseBytes, 0, responseBytes.Length, cancellationToken);
        await stream.FlushAsync(cancellationToken);
    }
}
