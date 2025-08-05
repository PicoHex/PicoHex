namespace Pico.Node.Http;

public class HttpHandler : ITcpHandler
{
    private static readonly byte[] _newLine = Encoding.UTF8.GetBytes("\r\n");
    private static readonly byte[] _headerSeparator = Encoding.UTF8.GetBytes(": ");
    private readonly ILogger<HttpHandler> _logger;

    public HttpHandler(ILogger<HttpHandler> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async ValueTask HandleAsync(
        NetworkStream stream,
        CancellationToken cancellationToken = default
    )
    {
        string? clientEndpoint = null;
        try
        {
            clientEndpoint = stream.Socket?.RemoteEndPoint?.ToString() ?? "unknown";
            await _logger.InfoAsync(
                $"HTTP connection started from {clientEndpoint}",
                cancellationToken
            );

            // Use a MemoryStream to buffer the entire request
            using var requestBuffer = new MemoryStream(4096);
            var headerEndFound = false;
            var buffer = new byte[4096];

            // Read until we find the end of headers (CRLFCRLF)
            while (!headerEndFound)
            {
                int bytesRead = await stream.ReadAsync(buffer, cancellationToken);
                if (bytesRead == 0)
                    break; // Client disconnected

                // Write to our buffer
                await requestBuffer.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);

                // Check if we have the full headers
                headerEndFound = ContainsSequence(
                    requestBuffer.GetBuffer(),
                    (int)requestBuffer.Position,
                    "\r\n\r\n"u8.ToArray()
                ); // \r\n\r\n
            }

            if (requestBuffer.Position == 0)
            {
                await _logger.WarningAsync(
                    $"Empty request from {clientEndpoint}",
                    cancellationToken: cancellationToken
                );
                await WriteResponseAsync(
                    stream,
                    400,
                    "Bad Request",
                    "Empty request",
                    cancellationToken
                );
                return;
            }

            // Convert buffer to string for processing
            var requestText = Encoding.UTF8.GetString(
                requestBuffer.GetBuffer(),
                0,
                (int)requestBuffer.Position
            );
            var requestLines = requestText.Split("\r\n");

            // First line is the request line
            if (requestLines.Length == 0)
            {
                await _logger.WarningAsync(
                    $"Invalid request from {clientEndpoint}: No request line",
                    cancellationToken: cancellationToken
                );
                await WriteResponseAsync(
                    stream,
                    400,
                    "Bad Request",
                    "Invalid request line",
                    cancellationToken
                );
                return;
            }

            var requestLine = requestLines[0];
            var requestParts = requestLine.Split(' ');
            if (requestParts.Length < 3)
            {
                await _logger.WarningAsync(
                    $"Invalid request line from {clientEndpoint}: {requestLine}",
                    cancellationToken: cancellationToken
                );
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

            // Handle request based on path
            if (path == "/")
            {
                await HandleRootRequest(stream, method, clientEndpoint, cancellationToken);
            }
            else if (path == "/time")
            {
                await HandleTimeRequest(stream, clientEndpoint, cancellationToken);
            }
            else if (path.StartsWith("/echo/"))
            {
                await HandleEchoRequest(stream, path[6..], clientEndpoint, cancellationToken);
            }
            else
            {
                await _logger.WarningAsync(
                    $"Path not found: {path} from {clientEndpoint}",
                    cancellationToken: cancellationToken
                );
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

    // Helper to check for byte sequence in buffer
    private bool ContainsSequence(byte[] buffer, int length, byte[] sequence)
    {
        if (length < sequence.Length)
            return false;

        for (int i = 0; i <= length - sequence.Length; i++)
        {
            bool match = true;
            for (int j = 0; j < sequence.Length; j++)
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

    private async ValueTask HandleRootRequest(
        NetworkStream stream,
        string method,
        string clientEndpoint,
        CancellationToken ct
    )
    {
        if (method != "GET")
        {
            await _logger.WarningAsync(
                $"Method not allowed from {clientEndpoint}: {method}",
                cancellationToken: ct
            );

            await WriteResponseAsync(
                stream,
                405,
                "Method Not Allowed",
                "Only GET is supported",
                ct
            );
            return;
        }

        string htmlContent =
            @"
            <html>
                <head><title>Pico HTTP Server</title></head>
                <body>
                    <h1>Welcome to Pico.Node HTTP Server</h1>
                    <p>Try these endpoints:</p>
                    <ul>
                        <li><a href='/echo/hello'>/echo/hello</a></li>
                        <li><a href='/time'>/time</a></li>
                    </ul>
                </body>
            </html>";

        await _logger.DebugAsync($"Serving root page to {clientEndpoint}", ct);
        await WriteResponseAsync(stream, 200, "OK", htmlContent, ct, "text/html");
    }

    private async ValueTask HandleEchoRequest(
        NetworkStream stream,
        string message,
        string clientEndpoint,
        CancellationToken ct
    )
    {
        await _logger.DebugAsync($"Echo request from {clientEndpoint}: {message}", ct);
        await WriteResponseAsync(stream, 200, "OK", $"You said: {message}", ct);
    }

    private async ValueTask HandleTimeRequest(
        NetworkStream stream,
        string clientEndpoint,
        CancellationToken ct
    )
    {
        var currentTime = DateTime.UtcNow.ToString("O");
        await _logger.DebugAsync($"Time request from {clientEndpoint}: {currentTime}", ct);
        await WriteResponseAsync(stream, 200, "OK", $"Current server time: {currentTime}", ct);
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
            byte[] bodyBytes = Encoding.UTF8.GetBytes(body);

            var headers = new Dictionary<string, string>
            {
                ["Date"] = DateTime.UtcNow.ToString("R"),
                ["Content-Type"] = contentType,
                ["Content-Length"] = bodyBytes.Length.ToString(),
                ["Connection"] = "close",
                ["Server"] = "Pico.Node/1.0"
            };

            var statusLine = $"HTTP/1.1 {statusCode} {statusText}\r\n";
            byte[] statusLineBytes = Encoding.UTF8.GetBytes(statusLine);

            // Check cancellation before each write
            ct.ThrowIfCancellationRequested();
            await stream.WriteAsync(statusLineBytes, ct);

            foreach (var header in headers)
            {
                ct.ThrowIfCancellationRequested();
                await stream.WriteAsync(Encoding.UTF8.GetBytes(header.Key), ct);
                await stream.WriteAsync(_headerSeparator, ct);
                await stream.WriteAsync(Encoding.UTF8.GetBytes(header.Value), ct);
                await stream.WriteAsync(_newLine, ct);
            }

            ct.ThrowIfCancellationRequested();
            await stream.WriteAsync(_newLine, ct);

            ct.ThrowIfCancellationRequested();
            await stream.WriteAsync(bodyBytes, ct);

            ct.ThrowIfCancellationRequested();
            await stream.FlushAsync(ct);

            await _logger.DebugAsync(
                $"Sent response to {stream.Socket?.RemoteEndPoint}: {statusCode} {statusText} "
                    + $"[Length: {bodyBytes.Length} bytes]",
                ct
            );
        }
        catch (OperationCanceledException)
        {
            // Normal cancellation - no need to log as error
            await _logger.DebugAsync("Response writing canceled by client request", ct);
        }
        catch (IOException ex)
            when (ex.InnerException is SocketException se
                && se.SocketErrorCode == SocketError.ConnectionAborted
            )
        {
            // Client disconnected before response completed
            await _logger.DebugAsync("Client aborted connection during response writing", ct);
        }
        catch (SocketException ex) when (ex.SocketErrorCode == SocketError.ConnectionAborted)
        {
            await _logger.DebugAsync("Client aborted connection during response writing", ct);
        }
        catch (Exception ex)
        {
            await _logger.ErrorAsync($"Error writing response: {ex.Message}", ex, ct);
            throw;
        }
    }
}
