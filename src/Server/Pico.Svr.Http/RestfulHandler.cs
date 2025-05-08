namespace Pico.Svr.Http;

public class RestfulHandler(ILogger<RestfulHandler> logger) : ITcpHandler
{
    private readonly ILogger<RestfulHandler> _logger =
        logger ?? throw new ArgumentNullException(nameof(logger));

    public async ValueTask HandleAsync(
        NetworkStream stream,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            var request = await ReadRequestAsync(stream, cancellationToken);
            var response = HandleRequest(request);
            await WriteResponseAsync(stream, response, cancellationToken);
        }
        catch (Exception ex)
        {
            await _logger.ErrorAsync(
                "Error handling RESTful request",
                ex,
                cancellationToken: cancellationToken
            );

            // Respond with a 500 error if something goes wrong
            try
            {
                var errorResponse = new HttpResponse
                {
                    StatusCode = 500,
                    StatusDescription = "Internal Server Error",
                    Headers = { ["Content-Length"] = "0" },
                    Body = []
                };
                await WriteResponseAsync(stream, errorResponse, cancellationToken);
            }
            catch
            {
                // Ignore any further exceptions
            }
        }
    }

    private HttpResponse HandleRequest(HttpRequest request)
    {
        try
        {
            return request.Method switch
            {
                "GET" => HandleGet(request),
                "POST" => HandlePost(request),
                "PUT" => HandlePut(request),
                "DELETE" => HandleDelete(request),
                _
                    => new HttpResponse
                    {
                        StatusCode = 405,
                        StatusDescription = "Method Not Allowed",
                        Headers = { ["Allow"] = "GET, POST, PUT, DELETE" },
                        Body = "Method Not Allowed"u8.ToArray()
                    }
            };
        }
        catch (Exception ex)
        {
            _logger.Error("Error processing request", ex);
            return new HttpResponse
            {
                StatusCode = 500,
                StatusDescription = "Internal Server Error",
                Headers = { ["Content-Length"] = "0" },
                Body = []
            };
        }
    }

    private HttpResponse HandleGet(HttpRequest request)
    {
        // Handle GET requests here
        return new HttpResponse
        {
            StatusCode = 200,
            StatusDescription = "OK",
            Headers = { ["Content-Type"] = "application/json" },
            Body = "{\"message\": \"GET request handled\"}"u8.ToArray()
        };
    }

    private HttpResponse HandlePost(HttpRequest request)
    {
        // Handle POST requests here
        return new HttpResponse
        {
            StatusCode = 201,
            StatusDescription = "Created",
            Headers = { ["Content-Type"] = "application/json" },
            Body = "{\"message\": \"POST request handled\"}"u8.ToArray()
        };
    }

    private HttpResponse HandlePut(HttpRequest request)
    {
        // Handle PUT requests here
        return new HttpResponse
        {
            StatusCode = 200,
            StatusDescription = "OK",
            Headers = { ["Content-Type"] = "application/json" },
            Body = "{\"message\": \"PUT request handled\"}"u8.ToArray()
        };
    }

    private HttpResponse HandleDelete(HttpRequest request)
    {
        // Handle DELETE requests here
        return new HttpResponse
        {
            StatusCode = 200,
            StatusDescription = "OK",
            Headers = { ["Content-Type"] = "application/json" },
            Body = "{\"message\": \"DELETE request handled\"}"u8.ToArray()
        };
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
        await stream.ReadExactlyAsync(buffer.AsMemory(0, contentLength), cancellationToken);
        request.Body = buffer;

        return request;
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
            await stream.WriteAsync(response.Body, cancellationToken);
        }

        await stream.FlushAsync(cancellationToken);
    }
}
