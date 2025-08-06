namespace Pico.Node.Http;

public class HttpHandler(ILogger<HttpHandler> logger) : ITcpHandler
{
    private static readonly byte[] NewLine = "\r\n"u8.ToArray();
    private static readonly byte[] HeaderSeparator = ": "u8.ToArray();
    private readonly ILogger<HttpHandler> _logger =
        logger ?? throw new ArgumentNullException(nameof(logger));

    // Route definitions with parameter support
    private readonly Dictionary<string, Func<HttpRequest, HttpResponse, ValueTask>> _routes =
        new()
        {
            { "GET /", HandleRootRequest },
            { "GET /time", HandleTimeRequest },
            { "GET /echo/{message}", HandleEchoRequest },
            { "POST /echo", HandleEchoRequest },
            { "GET /user/{id}", HandleUserRequest },
            { "PUT /user/{id}", HandleUserRequest }
        };

    public async ValueTask HandleAsync(
        NetworkStream stream,
        CancellationToken cancellationToken = default
    )
    {
        string? clientEndpoint = null;
        try
        {
            clientEndpoint = stream.Socket.RemoteEndPoint?.ToString() ?? "unknown";
            await _logger.InfoAsync(
                $"HTTP connection started from {clientEndpoint}",
                cancellationToken
            );

            // Read request headers
            var (requestLine, headers) = await ReadHeadersAsync(stream, cancellationToken);
            if (requestLine == null)
            {
                await WriteResponseAsync(
                    stream,
                    400,
                    "Bad Request",
                    "Invalid request",
                    cancellationToken
                );
                return;
            }

            // Parse request line
            var requestParts = requestLine.Split(' ', 3);
            if (requestParts.Length < 3)
            {
                await WriteResponseAsync(
                    stream,
                    400,
                    "Bad Request",
                    "Invalid request line",
                    cancellationToken
                );
                return;
            }

            var method = requestParts[0];
            var path = requestParts[1];
            var protocol = requestParts[2];

            await _logger.InfoAsync(
                $"Request from {clientEndpoint}: {method} {path} {protocol}",
                cancellationToken
            );

            // Read request body if exists
            var body = string.Empty;
            if (
                headers.TryGetValue("Content-Length", out var contentLengthValue)
                && int.TryParse(contentLengthValue, out var contentLength)
                && contentLength > 0
            )
            {
                body = await ReadBodyAsync(stream, contentLength, cancellationToken);
            }

            // Create request context
            var request = new HttpRequest
            {
                Method = method,
                Path = path,
                Protocol = protocol,
                Headers = headers,
                Body = body,
                ClientEndpoint = clientEndpoint
            };

            // Create response context
            var response = new HttpResponse();

            // Find matching route
            var routeHandler = FindRouteHandler(request.Method, request.Path);
            if (routeHandler != null)
            {
                await routeHandler(request, response);
            }
            else
            {
                response.StatusCode = 404;
                response.StatusText = "Not Found";
                response.Body = "Resource not found";
            }

            // Send response
            await WriteResponseAsync(
                stream,
                response.StatusCode,
                response.StatusText,
                response.Body,
                cancellationToken,
                response.ContentType
            );
        }
        catch (OperationCanceledException)
        {
            await _logger.InfoAsync($"Request canceled for {clientEndpoint}", cancellationToken);
        }
        catch (Exception ex)
        {
            try
            {
                await _logger.ErrorAsync(
                    $"Processing error for {clientEndpoint}: {ex.Message}",
                    ex,
                    cancellationToken: cancellationToken
                );

                await WriteResponseAsync(
                    stream,
                    500,
                    "Internal Server Error",
                    $"Error: {ex.Message}",
                    cancellationToken
                );
            }
            catch (Exception innerEx)
            {
                await _logger.ErrorAsync(
                    $"Secondary error while handling failure for {clientEndpoint}: {innerEx.Message}",
                    innerEx,
                    cancellationToken: cancellationToken
                );
            }
        }
        finally
        {
            await _logger.InfoAsync(
                $"HTTP connection completed for {clientEndpoint}",
                cancellationToken
            );
        }
    }

    private async Task<(string? RequestLine, Dictionary<string, string> Headers)> ReadHeadersAsync(
        NetworkStream stream,
        CancellationToken ct
    )
    {
        using var buffer = new MemoryStream(4096);
        var headerEndFound = false;
        var tempBuffer = new byte[4096];
        var headerBytes = new List<byte>();

        // Read until end of headers
        while (!headerEndFound)
        {
            int bytesRead = await stream.ReadAsync(tempBuffer, ct);
            if (bytesRead == 0)
                break;

            await buffer.WriteAsync(tempBuffer.AsMemory(0, bytesRead), ct);
            headerBytes.AddRange(tempBuffer.Take(bytesRead));

            // Check for end of headers
            if (ContainsSequence(headerBytes, "\r\n\r\n"u8.ToArray()))
            {
                headerEndFound = true;
            }
        }

        if (buffer.Length == 0)
            return (null, new Dictionary<string, string>());

        // Convert to string for parsing
        var headerText = Encoding.UTF8.GetString(headerBytes.ToArray());
        var headerLines = headerText.Split("\r\n");

        if (headerLines.Length == 0)
            return (null, new Dictionary<string, string>());

        // Parse headers
        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        for (int i = 1; i < headerLines.Length; i++)
        {
            if (string.IsNullOrEmpty(headerLines[i]))
                break;

            var separatorIndex = headerLines[i].IndexOf(':');
            if (separatorIndex > 0)
            {
                var key = headerLines[i][..separatorIndex].Trim();
                var value = headerLines[i][(separatorIndex + 1)..].Trim();
                headers[key] = value;
            }
        }

        return (headerLines[0], headers);
    }

    private async Task<string> ReadBodyAsync(
        NetworkStream stream,
        int contentLength,
        CancellationToken ct
    )
    {
        var bodyBytes = new byte[contentLength];
        int bytesRead = 0;

        while (bytesRead < contentLength)
        {
            int chunkSize = await stream.ReadAsync(
                bodyBytes.AsMemory(bytesRead, contentLength - bytesRead),
                ct
            );

            if (chunkSize == 0)
                break;
            bytesRead += chunkSize;
        }

        return Encoding.UTF8.GetString(bodyBytes);
    }

    private Func<HttpRequest, HttpResponse, ValueTask>? FindRouteHandler(string method, string path)
    {
        foreach (var route in _routes)
        {
            var routeParts = route.Key.Split(' ', 2);
            var routeMethod = routeParts[0];
            var routePathPattern = routeParts[1];

            // Skip if method doesn't match
            if (
                !string.Equals(routeMethod, method, StringComparison.OrdinalIgnoreCase)
                && routeMethod != "*"
            )
                continue;

            // Try to match path pattern with parameters
            var (isMatch, parameters) = MatchPath(routePathPattern, path);
            if (isMatch)
            {
                // Return handler with parameters bound
                return (request, response) =>
                {
                    request.RouteParameters = parameters;
                    return route.Value(request, response);
                };
            }
        }
        return null;
    }

    private (bool isMatch, Dictionary<string, string> parameters) MatchPath(
        string pattern,
        string actualPath
    )
    {
        var parameters = new Dictionary<string, string>();
        var patternSegments = pattern.Split('/');
        var actualSegments = actualPath.Split('/');

        if (patternSegments.Length != actualSegments.Length)
            return (false, parameters);

        for (int i = 0; i < patternSegments.Length; i++)
        {
            if (patternSegments[i].StartsWith('{') && patternSegments[i].EndsWith('}'))
            {
                // Extract parameter name
                var paramName = patternSegments[i][1..^1];
                parameters[paramName] = Uri.UnescapeDataString(actualSegments[i]);
            }
            else if (
                !string.Equals(
                    patternSegments[i],
                    actualSegments[i],
                    StringComparison.OrdinalIgnoreCase
                )
            )
            {
                return (false, parameters);
            }
        }

        return (true, parameters);
    }

    // Helper to check for byte sequence in buffer
    private bool ContainsSequence(IList<byte> buffer, byte[] sequence)
    {
        if (buffer.Count < sequence.Length)
            return false;

        for (var i = 0; i <= buffer.Count - sequence.Length; i++)
        {
            bool match = true;
            for (var j = 0; j < sequence.Length; j++)
            {
                if (buffer[i + j] != sequence[j])
                {
                    match = false;
                    break;
                }
            }
            if (match)
                return true;
        }
        return false;
    }

    // Route handlers
    private static async ValueTask HandleRootRequest(HttpRequest request, HttpResponse response)
    {
        if (request.Method != "GET")
        {
            response.StatusCode = 405;
            response.StatusText = "Method Not Allowed";
            response.Body = "Only GET is supported";
            return;
        }

        const string htmlContent = """
            <html>
                <head><title>Pico HTTP Server</title></head>
                <body>
                    <h1>Welcome to Pico.Node HTTP Server</h1>
                    <p>Try these endpoints:</p>
                    <ul>
                        <li><a href='/echo/hello'>GET /echo/hello</a></li>
                        <li><a href='/time'>GET /time</a></li>
                        <li>POST /echo (with body)</li>
                        <li><a href='/user/123'>GET /user/{id}</a></li>
                    </ul>
                </body>
            </html>
            """;

        response.StatusCode = 200;
        response.StatusText = "OK";
        response.Body = htmlContent;
        response.ContentType = "text/html";
    }

    private static ValueTask HandleEchoRequest(HttpRequest request, HttpResponse response)
    {
        string message;

        switch (request.Method)
        {
            case "GET" when request.RouteParameters.TryGetValue("message", out var pathMessage):
                message = pathMessage;
                break;
            case "POST":
                message = request.Body;
                break;
            default:
                response.StatusCode = 400;
                response.StatusText = "Bad Request";
                response.Body = "Invalid echo request";
                return ValueTask.CompletedTask;
        }

        response.StatusCode = 200;
        response.StatusText = "OK";
        response.Body = $"You said: {message}";
        return ValueTask.CompletedTask;
    }

    private static ValueTask HandleTimeRequest(HttpRequest request, HttpResponse response)
    {
        response.StatusCode = 200;
        response.StatusText = "OK";
        response.Body = $"Current server time: {DateTime.UtcNow:O}";
        return ValueTask.CompletedTask;
    }

    private static ValueTask HandleUserRequest(HttpRequest request, HttpResponse response)
    {
        if (
            !request.RouteParameters.TryGetValue("id", out var userId)
            || !int.TryParse(userId, out _)
        )
        {
            response.StatusCode = 400;
            response.StatusText = "Bad Request";
            response.Body = "Invalid user ID";
            return ValueTask.CompletedTask;
        }

        switch (request.Method)
        {
            case "GET":
                response.StatusCode = 200;
                response.StatusText = "OK";
                response.Body = $"User details for ID: {userId}";
                break;

            case "PUT":
                response.StatusCode = 200;
                response.StatusText = "OK";
                response.Body = $"Updated user with ID: {userId}\nRequest body: {request.Body}";
                break;

            default:
                response.StatusCode = 405;
                response.StatusText = "Method Not Allowed";
                response.Body = $"Unsupported method: {request.Method}";
                break;
        }

        return ValueTask.CompletedTask;
    }

    private async ValueTask WriteResponseAsync(
        NetworkStream stream,
        int statusCode,
        string statusText,
        string body,
        CancellationToken ct,
        string contentType = "text/plain"
    )
    {
        try
        {
            var bodyBytes = Encoding.UTF8.GetBytes(body);
            var responseBuilder = new StringBuilder();

            // Build status line
            responseBuilder.Append($"HTTP/1.1 {statusCode} {statusText}\r\n");

            // Build headers
            responseBuilder.Append($"Date: {DateTime.UtcNow:R}\r\n");
            responseBuilder.Append($"Content-Type: {contentType}\r\n");
            responseBuilder.Append($"Content-Length: {bodyBytes.Length}\r\n");
            responseBuilder.Append("Connection: close\r\n");
            responseBuilder.Append("Server: Pico.Node/1.0\r\n");
            responseBuilder.Append("\r\n"); // End of headers

            // Convert headers to bytes
            var headerBytes = Encoding.UTF8.GetBytes(responseBuilder.ToString());

            // Write headers
            ct.ThrowIfCancellationRequested();
            await stream.WriteAsync(headerBytes, ct);

            // Write body
            ct.ThrowIfCancellationRequested();
            await stream.WriteAsync(bodyBytes, ct);

            ct.ThrowIfCancellationRequested();
            await stream.FlushAsync(ct);
        }
        catch (OperationCanceledException)
        {
            // Normal cancellation
        }
        catch (IOException ex)
            when (ex.InnerException
                    is SocketException { SocketErrorCode: SocketError.ConnectionAborted }
            )
        {
            // Client disconnected
        }
        catch (SocketException ex) when (ex.SocketErrorCode == SocketError.ConnectionAborted)
        {
            // Client disconnected
        }
        catch (Exception ex)
        {
            await _logger.ErrorAsync($"Error writing response: {ex.Message}", ex, ct);
            throw;
        }
    }
}
