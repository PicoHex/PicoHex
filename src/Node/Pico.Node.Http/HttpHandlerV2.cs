namespace Pico.Node.Http;

/// <summary>
/// HTTP protocol handler implementing IPipelineHandler interface.
/// Handles HTTP/1.1 requests with support for persistent connections.
/// </summary>
public sealed class HttpHandlerV2 : IPipelineHandler, IAsyncDisposable
{
    // Constants for HTTP protocol elements
    private const string HttpVersion = "HTTP/1.1";
    private const string ServerHeader = "Pico.Node/1.0";
    private const int MaxRequestSize = 1024 * 1024; // 1MB
    private const int BufferSize = 4096;

    // Precompiled regex for request line parsing
    private static readonly Regex RequestLineRegex =
        new(
            @"^(GET|POST|PUT|DELETE|HEAD|OPTIONS|PATCH|TRACE)\s+([^\s]+)\s+" + HttpVersion,
            RegexOptions.Compiled | RegexOptions.IgnoreCase
        );

    // Response templates
    private static readonly byte[] CrLf = "\r\n"u8.ToArray();

    /// <summary>
    /// Handles HTTP communication pipeline for a TCP connection.
    /// </summary>
    public async Task HandleAsync(
        PipeReader reader,
        PipeWriter writer,
        CancellationToken cancellationToken
    )
    {
        // Use persistent connection until client closes or timeout
        while (!cancellationToken.IsCancellationRequested)
        {
            // Read and parse HTTP request
            var (requestValid, method, path, headers) = await ParseHttpRequestAsync(
                reader,
                cancellationToken
            );

            if (!requestValid)
                break; // Connection closed or invalid request

            // Process request and generate response
            var responseBody = GenerateResponse(method, path);
            await WriteHttpResponseAsync(writer, responseBody, cancellationToken);

            // Check for Connection: close header
            if (ShouldCloseConnection(headers))
            {
                break;
            }
        }
    }

    /// <summary>
    /// Parses HTTP request from the pipeline reader.
    /// </summary>
    private async Task<(
        bool valid,
        string method,
        string path,
        Dictionary<string, string> headers
    )> ParseHttpRequestAsync(PipeReader reader, CancellationToken cancellationToken)
    {
        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var method = string.Empty;
        var path = string.Empty;

        try
        {
            while (true)
            {
                // Read from the pipeline with cancellation support
                var result = await reader.ReadAsync(cancellationToken);
                var buffer = result.Buffer;

                // Check for end of connection
                if (buffer.IsEmpty && result.IsCompleted)
                    return (false, method, path, headers);

                // Parse request line
                if (TryParseRequestLine(ref buffer, out method, out path))
                {
                    // Parse headers
                    if (TryParseHeaders(ref buffer, headers))
                    {
                        // Calculate how much we consumed and advance reader
                        var consumed = buffer.GetPosition(0);
                        reader.AdvanceTo(consumed, buffer.End);
                        return (true, method, path, headers);
                    }
                }

                // Not enough data yet, continue reading
                reader.AdvanceTo(buffer.Start, buffer.End);

                // Enforce maximum request size
                if (buffer.Length > MaxRequestSize)
                    throw new InvalidOperationException(
                        $"Request exceeds maximum size: {MaxRequestSize} bytes"
                    );
            }
        }
        catch
        {
            return (false, method, path, headers);
        }
    }

    /// <summary>
    /// Parses HTTP request line (first line of request).
    /// </summary>
    private bool TryParseRequestLine(
        ref ReadOnlySequence<byte> buffer,
        out string method,
        out string path
    )
    {
        method = string.Empty;
        path = string.Empty;

        // Try to find the end of request line (CRLF)
        var lineEnd = buffer.PositionOf((byte)'\n');
        if (!lineEnd.HasValue)
            return false;

        // Extract request line
        var line = buffer.Slice(0, lineEnd.Value);
        buffer = buffer.Slice(buffer.GetPosition(1, lineEnd.Value));

        // Convert to string and parse
        var requestLine = GetAsciiString(line);
        var match = RequestLineRegex.Match(requestLine);

        if (!match.Success)
            return false;
        method = match.Groups[1].Value.ToUpperInvariant();
        path = match.Groups[2].Value;
        return true;
    }

    /// <summary>
    /// Parses HTTP headers until empty line is found.
    /// </summary>
    private bool TryParseHeaders(
        ref ReadOnlySequence<byte> buffer,
        Dictionary<string, string> headers
    )
    {
        while (true)
        {
            var lineEnd = buffer.PositionOf((byte)'\n');
            if (!lineEnd.HasValue)
                return false;

            var line = buffer.Slice(0, lineEnd.Value);
            buffer = buffer.Slice(buffer.GetPosition(1, lineEnd.Value));

            // Check for empty line (end of headers)
            if (line.Length <= 2) // CRLF or LF
            {
                return true;
            }

            // Parse header line
            var colonIndex = -1;
            foreach (var segment in line)
            {
                var span = segment.Span;
                for (var i = 0; i < span.Length; i++)
                {
                    if (span[i] != (byte)':')
                        continue;
                    colonIndex = i;
                    break;
                }
                if (colonIndex != -1)
                    break;
            }

            if (colonIndex == -1)
                continue;
            var headerName = GetAsciiString(line.Slice(0, colonIndex)).Trim();
            var headerValue = GetAsciiString(line.Slice(colonIndex + 1)).Trim();
            headers[headerName] = headerValue;
        }
    }

    /// <summary>
    /// Generates appropriate HTTP response based on request.
    /// </summary>
    private byte[] GenerateResponse(string method, string path)
    {
        // Handle echo path
        if (path.StartsWith("/echo/", StringComparison.Ordinal))
        {
            var message = path.Length > 6 ? path.Substring(6) : "";
            var response = $"Method: {method}\nPath: {path}\nMessage: {message}";
            return BuildResponse(200, "OK", "text/plain", response);
        }

        // Handle time path
        if (path == "/time" && method == "GET")
        {
            var currentTime = DateTime.UtcNow.ToString("O");
            return BuildResponse(200, "OK", "text/plain", currentTime);
        }

        switch (path)
        {
            // Health check endpoint
            case "/health":
                return BuildResponse(200, "OK", "text/plain", "Healthy");

            // Root endpoint with HTML content
            case "/" when method == "GET":
            {
                var html =
                    @"<html>
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
</html>";
                return BuildResponse(200, "OK", "text/html; charset=utf-8", html);
            }

            // Default 404 response
            default:
                return BuildResponse(404, "Not Found", "text/plain", "Resource not found");
        }
    }

    /// <summary>
    /// Builds complete HTTP response bytes.
    /// </summary>
    private byte[] BuildResponse(int statusCode, string statusText, string contentType, string body)
    {
        var bodyBytes = Encoding.UTF8.GetBytes(body);
        var response = new MemoryStream();

        // Response line
        WriteAscii(response, $"{HttpVersion} {statusCode} {statusText}\r\n");

        // Headers
        WriteAscii(response, $"Server: {ServerHeader}\r\n");
        WriteAscii(response, $"Content-Type: {contentType}\r\n");
        WriteAscii(response, $"Content-Length: {bodyBytes.Length}\r\n");
        WriteAscii(response, $"Date: {DateTime.UtcNow:R}\r\n");
        WriteAscii(response, "Connection: keep-alive\r\n");

        // End of headers
        response.Write(CrLf);

        // Response body
        response.Write(bodyBytes);

        return response.ToArray();
    }

    /// <summary>
    /// Writes HTTP response to the pipeline writer.
    /// </summary>
    private async ValueTask WriteHttpResponseAsync(
        PipeWriter writer,
        byte[] responseBytes,
        CancellationToken cancellationToken
    )
    {
        // Write response to pipeline
        var buffer = writer.GetMemory(responseBytes.Length);
        responseBytes.CopyTo(buffer);
        writer.Advance(responseBytes.Length);

        // Flush to network
        var flushResult = await writer.FlushAsync(cancellationToken);

        // Handle connection closure
        if (flushResult.IsCompleted || flushResult.IsCanceled)
        {
            throw new OperationCanceledException("Connection closed during response write");
        }
    }

    /// <summary>
    /// Checks if connection should be closed based on headers.
    /// </summary>
    private static bool ShouldCloseConnection(Dictionary<string, string> headers)
    {
        return headers.TryGetValue("Connection", out var connection)
            && "close".Equals(connection, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Converts ReadOnlySequence<byte> to ASCII string.
    /// </summary>
    private static string GetAsciiString(ReadOnlySequence<byte> sequence)
    {
        if (sequence.IsSingleSegment)
        {
            return Encoding.ASCII.GetString(sequence.FirstSpan);
        }

        return string.Create(
            (int)sequence.Length,
            sequence,
            (span, state) =>
            {
                foreach (var segment in state)
                {
                    Encoding.ASCII.GetChars(segment.Span, span);
                    span = span[segment.Length..];
                }
            }
        );
    }

    /// <summary>
    /// Writes ASCII string to stream without allocations.
    /// </summary>
    private static void WriteAscii(Stream stream, string text)
    {
        byte[]? buffer = null;
        try
        {
            var maxBytes = Encoding.ASCII.GetMaxByteCount(text.Length);
            buffer = ArrayPool<byte>.Shared.Rent(maxBytes);

            var bytesWritten = Encoding.ASCII.GetBytes(text, buffer);
            stream.Write(buffer, 0, bytesWritten);
        }
        finally
        {
            if (buffer != null)
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }
        }
    }

    /// <summary>
    /// Cleans up resources when handler is disposed.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        // Clean up any managed resources here
        await Task.CompletedTask;
    }
}
