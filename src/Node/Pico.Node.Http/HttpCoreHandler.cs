namespace Pico.Node.Http;

/// <summary>
/// HTTP server pipeline handler that handles core HTTP protocol responsibilities
///
/// Responsibilities:
/// 1. HTTP request parsing (request line, headers, body)
/// 2. HTTP response generation (status line, headers, body)
/// 3. Keep-Alive connection management
/// 4. Protocol-compliant error handling
///
/// Non-responsibilities:
/// - Routing
/// - Business logic
/// - Static file serving
/// - Authentication
/// - Advanced features (compression, caching, etc.)
/// </summary>
public class HttpCoreHandler : IPipelineHandler
{
    /// <summary>
    /// Processes a single TCP connection as an HTTP conversation
    /// </summary>
    public async Task HandleAsync(
        PipeReader reader,
        PipeWriter writer,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            while (true)
            {
                // Parse HTTP request from pipeline
                var request = await ParseHttpRequestAsync(reader, cancellationToken);
                if (request is null)
                    break; // Connection closed

                // Create response objects
                var context = new HttpContext(request, new HttpResponse());

                // Process request (application-specific logic would be injected here)
                await ProcessRequestAsync(context);

                // Write HTTP response to pipeline
                await WriteHttpResponseAsync(writer, context.Response, cancellationToken);

                // Close connection if Keep-Alive not requested
                if (!ShouldKeepAlive(request))
                    break;
            }
        }
        catch (OperationCanceledException)
        {
            // Graceful shutdown
        }
        finally
        {
            // Ensure proper resource cleanup
            await reader.CompleteAsync();
            await writer.CompleteAsync();
        }
    }

    /// <summary>
    /// Parses HTTP request from PipeReader
    /// </summary>
    private async Task<HttpRequest?> ParseHttpRequestAsync(
        PipeReader reader,
        CancellationToken cancellationToken
    )
    {
        while (true)
        {
            // Read from the pipeline
            var result = await reader.ReadAsync(cancellationToken);
            var buffer = result.Buffer;

            // Try to parse the request
            if (TryParseRequest(buffer, out var request, out var consumed))
            {
                // Advance reader position
                reader.AdvanceTo(consumed, buffer.End);
                return request;
            }

            // Not enough data - advance and continue
            reader.AdvanceTo(buffer.Start, buffer.End);

            // Stop if connection closed
            if (result.IsCompleted)
                return null;
        }
    }

    /// <summary>
    /// Attempts to parse a complete HTTP request from buffer
    /// </summary>
    private static bool TryParseRequest(
        ReadOnlySequence<byte> buffer,
        out HttpRequest? request,
        out SequencePosition consumed
    )
    {
        // Initialize defaults
        request = null;
        consumed = buffer.Start;

        // Create sequence reader
        var reader = new SequenceReader<byte>(buffer);

        // 1. Parse request line (METHOD URI VERSION)
        if (!reader.TryReadTo(out ReadOnlySpan<byte> requestLine, (byte)'\n'))
            return false;

        // Split request line components
        var parts = Encoding.ASCII.GetString(requestLine).Split(' ', 3);
        if (parts.Length < 3)
            return false;

        // Create request object
        request = new HttpRequest
        {
            Method = parts[0].Trim(),
            Path = parts[1].Trim(),
            Version = parts[2].Trim()
        };

        // 2. Parse headers (Key: Value)
        while (true)
        {
            if (!reader.TryReadTo(out ReadOnlySpan<byte> headerLine, (byte)'\n'))
                return false;

            // Empty line indicates end of headers
            if (headerLine.Length == 0 || (headerLine.Length == 1 && headerLine[0] == '\r'))
                break;

            // Parse header key-value pair
            var separator = headerLine.IndexOf((byte)':');
            if (separator <= 0)
                continue;

            var key = Encoding.ASCII.GetString(headerLine[..separator]).Trim();
            var value = Encoding.ASCII.GetString(headerLine[(separator + 1)..]).Trim();

            request.Headers[key] = value;
        }

        // 3. Parse body (if exists)
        if (
            request.Headers.TryGetValue("Content-Length", out var contentLengthStr)
            && int.TryParse(contentLengthStr, out var contentLength)
        )
        {
            // Check if full body is available
            if (reader.UnreadSequence.Length < contentLength)
                return false;

            // Capture body data
            request.Body = reader.UnreadSequence.Slice(0, contentLength).ToArray();
            reader.Advance(contentLength);
        }

        // Update consumed position
        consumed = reader.Position;
        return true;
    }

    /// <summary>
    /// Writes HTTP response to PipeWriter
    /// </summary>
    private static async Task WriteHttpResponseAsync(
        PipeWriter writer,
        HttpResponse response,
        CancellationToken cancellationToken
    )
    {
        // Write status line: HTTP/1.1 200 OK\r\n
        WriteAscii(
            writer,
            $"{response.Version} {response.StatusCode} {response.StatusDescription}\r\n"
        );

        // Write headers
        foreach (var (key, value) in response.Headers)
        {
            WriteAscii(writer, $"{key}: {value}\r\n");
        }

        // Write content length if body exists
        if (response.Body.Length > 0)
        {
            WriteAscii(writer, $"Content-Length: {response.Body.Length}\r\n");
        }

        // End of headers
        WriteAscii(writer, "\r\n");

        // Write body if exists
        if (response.Body.Length > 0)
        {
            await writer.WriteAsync(response.Body, cancellationToken);
        }

        // Flush all data to network
        await writer.FlushAsync(cancellationToken);
    }

    /// <summary>
    /// Helper to write ASCII text to PipeWriter
    /// </summary>
    private static void WriteAscii(PipeWriter writer, string text)
    {
        var length = Encoding.ASCII.GetByteCount(text);
        var buffer = writer.GetSpan(length);
        Encoding.ASCII.GetBytes(text, buffer);
        writer.Advance(length);
    }

    /// <summary>
    /// Determines if connection should be kept alive
    /// </summary>
    private static bool ShouldKeepAlive(HttpRequest request)
    {
        return request.Headers.TryGetValue("Connection", out var connection)
            && "keep-alive".Equals(connection, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Placeholder for application-specific request processing
    /// (In real implementation, this would be delegated to external handler)
    /// </summary>
    private static Task ProcessRequestAsync(HttpContext context)
    {
        // This would be replaced with actual application logic
        context.Response.StatusCode = 200;
        context.Response.StatusDescription = "OK";
        context.Response.Version = "HTTP/1.1";
        context.Response.Headers["Content-Type"] = "text/plain";
        context.Response.Body = "Hello from core HTTP server"u8.ToArray();
        return Task.CompletedTask;
    }
}

// ====================
// SUPPORTING TYPES
// ====================

/// <summary>
/// Represents HTTP request
/// </summary>
public class HttpRequest
{
    public string Method { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public string Version { get; set; } = "HTTP/1.1";
    public Dictionary<string, string> Headers { get; } = new(StringComparer.OrdinalIgnoreCase);
    public byte[] Body { get; set; } = [];
}

/// <summary>
/// Represents HTTP response
/// </summary>
public class HttpResponse
{
    public string Version { get; set; } = "HTTP/1.1";
    public int StatusCode { get; set; } = 200;
    public string StatusDescription { get; set; } = "OK";
    public Dictionary<string, string> Headers { get; } = new(StringComparer.OrdinalIgnoreCase);
    public byte[] Body { get; set; } = [];
}

/// <summary>
/// HTTP context containing request and response
/// </summary>
public class HttpContext(HttpRequest request, HttpResponse response)
{
    public HttpRequest Request { get; } = request;
    public HttpResponse Response { get; } = response;
}
