namespace Pico.Node.Http;

public class HttpHandler(ILogger<HttpHandler> logger) : ITcpHandler
{
    // Supported HTTP methods
    private static readonly HashSet<string> SupportedMethods =
        new(StringComparer.OrdinalIgnoreCase) { "GET", "POST", "PUT", "DELETE" };

    public async ValueTask HandleAsync(
        NetworkStream stream,
        CancellationToken cancellationToken = default
    )
    {
        string? clientEndpoint = null;
        try
        {
            clientEndpoint = stream.Socket.RemoteEndPoint?.ToString() ?? "unknown";
            await logger.InfoAsync(
                $"HTTP connection started from {clientEndpoint}",
                cancellationToken
            );

            // Parse request headers
            var (headers, requestLine, buffer, bytesRead) = await ParseHeadersAsync(
                stream,
                cancellationToken
            );

            if (requestLine == null)
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

            // Parse request line
            var requestParts = requestLine.Split(' ', 3);
            if (requestParts.Length < 3)
            {
                await WriteResponseAsync(
                    stream,
                    400,
                    "Bad Request",
                    "Invalid request line format",
                    cancellationToken
                );
                return;
            }

            var method = requestParts[0];
            var path = requestParts[1];
            var protocol = requestParts[2];

            // Validate HTTP method
            if (!SupportedMethods.Contains(method))
            {
                await WriteResponseAsync(
                    stream,
                    405,
                    "Method Not Allowed",
                    $"Unsupported method: {method}",
                    cancellationToken,
                    headers: new Dictionary<string, string>
                    {
                        ["Allow"] = string.Join(", ", SupportedMethods)
                    }
                );
                return;
            }

            await logger.InfoAsync(
                $"Request from {clientEndpoint}: {method} {path} {protocol}",
                cancellationToken
            );

            // Read request body if present
            var requestBody = string.Empty;
            if (
                headers.TryGetValue("Content-Length", out var contentLengthValue)
                && int.TryParse(contentLengthValue, out var contentLength)
                && contentLength > 0
            )
            {
                requestBody = await ReadRequestBodyAsync(
                    stream,
                    buffer,
                    bytesRead,
                    contentLength,
                    cancellationToken
                );
            }

            // Create request context
            var request = new HttpRequest
            {
                Method = method,
                Path = path,
                Headers = headers,
                Body = requestBody,
                ClientEndpoint = clientEndpoint
            };

            // Route handling with parameter support
            var routeParams = new Dictionary<string, string>();
            var handler = FindRouteHandler(path, routeParams);

            if (handler != null)
            {
                await handler(stream, request, routeParams, cancellationToken);
            }
            else
            {
                await WriteResponseAsync(
                    stream,
                    404,
                    "Not Found",
                    "Resource not found",
                    cancellationToken
                );
            }
        }
        catch (OperationCanceledException)
        {
            await logger.InfoAsync($"Request canceled for {clientEndpoint}", cancellationToken);
        }
        catch (Exception ex)
        {
            try
            {
                await logger.ErrorAsync(
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
                await logger.ErrorAsync(
                    $"Secondary error while handling failure for {clientEndpoint}: {innerEx.Message}",
                    innerEx,
                    cancellationToken: cancellationToken
                );
            }
        }
        finally
        {
            await logger.InfoAsync(
                $"HTTP connection completed for {clientEndpoint}",
                cancellationToken
            );
        }
    }

    private async Task<(
        Dictionary<string, string> Headers,
        string? RequestLine,
        byte[] Buffer,
        int TotalBytes
    )> ParseHeadersAsync(NetworkStream stream, CancellationToken ct)
    {
        using var headerBuffer = new MemoryStream(4096);
        var buffer = new byte[4096];
        var headerEndFound = false;
        var totalBytes = 0;

        while (!headerEndFound)
        {
            var bytesRead = await stream.ReadAsync(buffer, ct);
            if (bytesRead == 0)
                break;

            totalBytes += bytesRead;
            await headerBuffer.WriteAsync(buffer.AsMemory(0, bytesRead), ct);

            headerEndFound = ContainsSequence(
                headerBuffer.GetBuffer(),
                (int)headerBuffer.Position,
                "\r\n\r\n"u8.ToArray()
            );
        }

        if (headerBuffer.Position == 0)
            return (new Dictionary<string, string>(), null, buffer, totalBytes);

        var headerText = Encoding.UTF8.GetString(
            headerBuffer.GetBuffer(),
            0,
            (int)headerBuffer.Position
        );

        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        using var reader = new StringReader(headerText);

        // Parse request line
        var requestLine = await reader.ReadLineAsync(ct);
        if (string.IsNullOrEmpty(requestLine))
            return (headers, null, buffer, totalBytes);

        // Parse headers
        string? line;
        while (!string.IsNullOrEmpty(line = await reader.ReadLineAsync(ct)))
        {
            var separatorIndex = line.IndexOf(':');
            if (separatorIndex <= 0)
                continue;

            var key = line[..separatorIndex].Trim();
            var value = line[(separatorIndex + 1)..].Trim();

            if (!string.IsNullOrEmpty(key))
            {
                headers[key] = value;
            }
        }

        return (headers, requestLine, buffer, totalBytes);
    }

    private async Task<string> ReadRequestBodyAsync(
        NetworkStream stream,
        byte[] buffer,
        int alreadyRead,
        int contentLength,
        CancellationToken ct
    )
    {
        using var bodyBuffer = new MemoryStream(contentLength);

        // Write already buffered data (if any)
        if (alreadyRead > 0)
        {
            var remainingInBuffer = Math.Min(alreadyRead, contentLength);
            await bodyBuffer.WriteAsync(buffer.AsMemory(0, remainingInBuffer), ct);
        }

        // Read remaining content
        var remaining = contentLength - (int)bodyBuffer.Length;
        while (remaining > 0)
        {
            var bytesToRead = Math.Min(buffer.Length, remaining);
            var bytesRead = await stream.ReadAsync(buffer, 0, bytesToRead, ct);
            if (bytesRead == 0)
                break;

            await bodyBuffer.WriteAsync(buffer.AsMemory(0, bytesRead), ct);
            remaining -= bytesRead;
        }

        return Encoding.UTF8.GetString(bodyBuffer.GetBuffer(), 0, (int)bodyBuffer.Position);
    }

    private delegate ValueTask RouteHandler(
        NetworkStream stream,
        HttpRequest request,
        Dictionary<string, string> routeParams,
        CancellationToken ct
    );

    private RouteHandler? FindRouteHandler(string path, Dictionary<string, string> routeParams)
    {
        switch (path)
        {
            // Root endpoint
            case "/":
                return HandleRootRequest;
            // Time endpoint
            case "/time":
                return HandleTimeRequest;
        }

        // Echo endpoint with parameter
        if (path.StartsWith("/echo/", StringComparison.Ordinal))
        {
            routeParams["message"] = path.Substring(6);
            return HandleEchoRequest;
        }

        // User creation endpoint
        if (!path.StartsWith("/users/", StringComparison.Ordinal) || path.Length <= 7)
            return null;
        routeParams["userId"] = path[7..];
        return HandleUserRequest;
    }

    // Helper to check for byte sequence in buffer
    private static bool ContainsSequence(byte[] buffer, int length, byte[] sequence)
    {
        if (length < sequence.Length)
            return false;

        for (var i = 0; i <= length - sequence.Length; i++)
        {
            var match = !sequence.Where((t, j) => buffer[i + j] != t).Any();
            if (match)
                return true;
        }
        return false;
    }

    private async ValueTask HandleRootRequest(
        NetworkStream stream,
        HttpRequest request,
        Dictionary<string, string> routeParams,
        CancellationToken ct
    )
    {
        // Only GET allowed for root
        if (!request.Method.Equals("GET", StringComparison.OrdinalIgnoreCase))
        {
            await WriteResponseAsync(
                stream,
                405,
                "Method Not Allowed",
                "Only GET is supported for this resource",
                ct,
                headers: new Dictionary<string, string> { ["Allow"] = "GET" }
            );
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
                        <li>POST /users/{id} (with JSON body)</li>
                    </ul>
                </body>
            </html>
            """;

        await WriteResponseAsync(stream, 200, "OK", htmlContent, ct, "text/html");
    }

    private async ValueTask HandleEchoRequest(
        NetworkStream stream,
        HttpRequest request,
        Dictionary<string, string> routeParams,
        CancellationToken ct
    )
    {
        // All methods allowed for echo
        var message = routeParams.TryGetValue("message", out var msg) ? msg : string.Empty;
        var response = $"Method: {request.Method}\nPath: {request.Path}\nMessage: {message}";
        await WriteResponseAsync(stream, 200, "OK", response, ct);
    }

    private async ValueTask HandleTimeRequest(
        NetworkStream stream,
        HttpRequest request,
        Dictionary<string, string> routeParams,
        CancellationToken ct
    )
    {
        // Only GET allowed for time
        if (!request.Method.Equals("GET", StringComparison.OrdinalIgnoreCase))
        {
            await WriteResponseAsync(
                stream,
                405,
                "Method Not Allowed",
                "Only GET is supported for this resource",
                ct,
                headers: new Dictionary<string, string> { ["Allow"] = "GET" }
            );
            return;
        }

        var currentTime = DateTime.UtcNow.ToString("O");
        await WriteResponseAsync(stream, 200, "OK", currentTime, ct);
    }

    private async ValueTask HandleUserRequest(
        NetworkStream stream,
        HttpRequest request,
        Dictionary<string, string> routeParams,
        CancellationToken ct
    )
    {
        if (!routeParams.TryGetValue("userId", out var userId))
        {
            await WriteResponseAsync(stream, 400, "Bad Request", "Missing user ID", ct);
            return;
        }

        switch (request.Method.ToUpperInvariant())
        {
            case "GET":
                await WriteResponseAsync(stream, 200, "OK", $"User details for {userId}", ct);
                break;

            case "POST":
            case "PUT":
                await WriteResponseAsync(
                    stream,
                    200,
                    "OK",
                    $"Updated user {userId} with data: {request.Body}",
                    ct,
                    "application/json"
                );
                break;

            case "DELETE":
                await WriteResponseAsync(stream, 200, "OK", $"Deleted user {userId}", ct);
                break;

            default:
                await WriteResponseAsync(
                    stream,
                    405,
                    "Method Not Allowed",
                    $"Unsupported method: {request.Method}",
                    ct,
                    headers: new Dictionary<string, string> { ["Allow"] = "GET, POST, PUT, DELETE" }
                );
                break;
        }
    }

    private async ValueTask WriteResponseAsync(
        NetworkStream stream,
        int statusCode,
        string statusText,
        string body,
        CancellationToken ct,
        string contentType = "text/plain",
        Dictionary<string, string>? headers = null
    )
    {
        try
        {
            var bodyBytes = Encoding.UTF8.GetBytes(body);
            var headerDict = headers ?? new Dictionary<string, string>();

            // Use StringBuilder for efficient header construction
            var headerBuilder = new StringBuilder();
            headerBuilder.Append($"HTTP/1.1 {statusCode} {statusText}\r\n");

            // Standard headers
            headerBuilder.Append($"Date: {DateTime.UtcNow.ToString("R")}\r\n");
            headerBuilder.Append($"Content-Type: {contentType}\r\n");
            headerBuilder.Append($"Content-Length: {bodyBytes.Length}\r\n");
            headerBuilder.Append("Connection: close\r\n");
            headerBuilder.Append("Server: Pico.Node/2.0\r\n");

            // Custom headers
            foreach (var header in headerDict)
            {
                headerBuilder.Append($"{header.Key}: {header.Value}\r\n");
            }

            // End of headers
            headerBuilder.Append("\r\n");

            var headerBytes = Encoding.UTF8.GetBytes(headerBuilder.ToString());

            // Write headers and body
            ct.ThrowIfCancellationRequested();
            await stream.WriteAsync(headerBytes, ct);
            await stream.WriteAsync(bodyBytes, ct);
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
            await logger.ErrorAsync($"Error writing response: {ex.Message}", ex, ct);
            throw;
        }
    }
}

// Encapsulates HTTP request data
public class HttpRequest
{
    public required string Method { get; set; }
    public required string Path { get; set; }
    public required Dictionary<string, string> Headers { get; set; }
    public string Body { get; set; } = string.Empty;
    public required string ClientEndpoint { get; set; }
}
