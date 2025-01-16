namespace PicoHex.Server.Http;

public class HttpHandler(ILogger<HttpHandler> logger) : ITcpHandler
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

            // Process the request
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
                var errorResponse = new HttpResponse
                {
                    StatusCode = 500,
                    StatusDescription = "Internal Server Error",
                    Headers = { ["Content-Length"] = "0" },
                    Body =  []
                };
                await WriteResponseAsync(stream, errorResponse, cancellationToken);
            }
            catch
            {
                // Suppress any exceptions while writing error response
            }
        }
    }

    private async Task<HttpRequest> ReadRequestAsync(
        NetworkStream stream,
        CancellationToken cancellationToken
    )
    {
        using var reader = new StreamReader(stream, Encoding.UTF8, leaveOpen: true);

        var requestLine = await reader.ReadLineAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(requestLine))
            throw new InvalidOperationException("Empty request line");

        var parts = requestLine.Split(' ');
        if (parts.Length != 3)
            throw new InvalidOperationException("Invalid request line");

        var request = new HttpRequest
        {
            Method = parts[0],
            Path = parts[1],
            ProtocolVersion = parts[2]
        };

        string? headerLine;
        while (
            !string.IsNullOrWhiteSpace(headerLine = await reader.ReadLineAsync(cancellationToken))
        )
        {
            var headerParts = headerLine.Split(':', 2);
            if (headerParts.Length == 2)
            {
                request.Headers[headerParts[0].Trim()] = headerParts[1].Trim();
            }
        }

        if (
            !request.Headers.TryGetValue("Content-Length", out var contentLengthValue)
            || !int.TryParse(contentLengthValue, out var contentLength)
            || contentLength <= 0
        )
            return request;
        var buffer = new byte[contentLength];
        await stream.ReadExactlyAsync(buffer, 0, contentLength, cancellationToken);
        request.Body = buffer;

        return request;
    }

    private HttpResponse ProcessRequest(HttpRequest request)
    {
        // Basic example: Always return a 200 OK response
        const string responseBody = "<html><body><h1>Hello, World!</h1></body></html>";
        return new HttpResponse
        {
            StatusCode = 200,
            StatusDescription = "OK",
            Headers =
            {
                ["Content-Type"] = "text/html",
                ["Content-Length"] = responseBody.Length.ToString()
            },
            Body = Encoding.UTF8.GetBytes(responseBody)
        };
    }

    private async Task WriteResponseAsync(
        NetworkStream stream,
        HttpResponse response,
        CancellationToken cancellationToken
    )
    {
        var responseBuilder = new StringBuilder();

        responseBuilder.AppendLine(
            $"{response.ProtocolVersion} {response.StatusCode} {response.StatusDescription}"
        );
        foreach (var header in response.Headers)
        {
            responseBuilder.AppendLine($"{header.Key}: {header.Value}");
        }
        responseBuilder.AppendLine();

        var responseHeaderBytes = Encoding.UTF8.GetBytes(responseBuilder.ToString());
        await stream.WriteAsync(responseHeaderBytes, cancellationToken);

        if (response.Body.Length > 0)
        {
            await stream.WriteAsync(
                response.Body.AsMemory(0, response.Body.Length),
                cancellationToken
            );
        }

        await stream.FlushAsync(cancellationToken);
    }
}
