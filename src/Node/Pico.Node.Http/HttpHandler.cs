namespace Pico.Node.Http;

/// <summary>
/// HTTP protocol handler that implements the ITcpHandler interface
/// for processing HTTP requests over TCP connections
/// </summary>
public class HttpHandler : ITcpHandler
{
    private readonly ILogger<HttpHandler> _logger;

    /// <summary>
    /// Initializes a new instance of the HttpHandler class
    /// </summary>
    /// <param name="logger">Logger instance for recording events and errors</param>
    public HttpHandler(ILogger<HttpHandler> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Processes HTTP requests and generates appropriate responses
    /// </summary>
    /// <param name="reader">PipeReader for reading incoming HTTP requests</param>
    /// <param name="writer">PipeWriter for writing HTTP responses</param>
    /// <param name="remoteEndPoint">Remote endpoint information of the client</param>
    /// <param name="ct">Cancellation token for aborting the operation</param>
    /// <returns>Task representing the asynchronous operation</returns>
    public async Task HandleAsync(
        PipeReader reader,
        PipeWriter writer,
        IPEndPoint remoteEndPoint,
        CancellationToken ct
    )
    {
        try
        {
            await _logger.InfoAsync(
                $"HTTP connection established from {remoteEndPoint}",
                cancellationToken: ct
            );

            while (!ct.IsCancellationRequested)
            {
                // Read HTTP request from the client
                var requestResult = await ReadHttpRequestAsync(reader, ct);
                if (requestResult.IsCompleted)
                {
                    await _logger.InfoAsync(
                        "HTTP connection closed by client",
                        cancellationToken: ct
                    );
                    break;
                }

                // Process the HTTP request and generate response
                var response = await ProcessHttpRequestAsync(
                    requestResult.Buffer,
                    remoteEndPoint,
                    ct
                );

                // Write HTTP response to the client
                await WriteHttpResponseAsync(writer, response, ct);

                // Advance the reader to indicate how much data was consumed
                reader.AdvanceTo(requestResult.Buffer.End);

                // For HTTP/1.0 or if Connection: close header is present, close the connection
                if (ShouldCloseConnection(requestResult.Buffer))
                {
                    await _logger.InfoAsync(
                        "Closing HTTP connection as requested",
                        cancellationToken: ct
                    );
                    break;
                }
            }
        }
        catch (OperationCanceledException)
        {
            await _logger.InfoAsync("HTTP handler operation cancelled", cancellationToken: ct);
        }
        catch (Exception ex)
        {
            await _logger.ErrorAsync("Error processing HTTP request", ex, cancellationToken: ct);

            // Try to send error response if possible
            try
            {
                await WriteErrorResponseAsync(writer, ex, ct);
            }
            catch
            {
                // Ignore any errors during error response writing
            }
        }
        finally
        {
            await _logger.InfoAsync(
                $"HTTP connection terminated for {remoteEndPoint}",
                cancellationToken: ct
            );
        }
    }

    /// <summary>
    /// Reads an HTTP request from the PipeReader
    /// </summary>
    /// <param name="reader">PipeReader to read from</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>ReadResult containing the HTTP request data</returns>
    private async ValueTask<ReadResult> ReadHttpRequestAsync(
        PipeReader reader,
        CancellationToken ct
    )
    {
        while (!ct.IsCancellationRequested)
        {
            var result = await reader.ReadAsync(ct);

            // Check if we have a complete HTTP request
            if (TryParseHttpRequest(result.Buffer, out var position))
            {
                return result;
            }

            // Tell the reader we've examined the entire buffer
            reader.AdvanceTo(result.Buffer.Start, result.Buffer.End);

            if (result.IsCompleted)
            {
                return result;
            }
        }

        return new ReadResult(default, false, true);
    }

    /// <summary>
    /// Attempts to parse an HTTP request from the buffer
    /// </summary>
    /// <param name="buffer">Buffer containing the HTTP data</param>
    /// <param name="position">Position where the request ends</param>
    /// <returns>True if a complete HTTP request was found</returns>
    private bool TryParseHttpRequest(ReadOnlySequence<byte> buffer, out SequencePosition position)
    {
        position = default;
        var reader = new SequenceReader<byte>(buffer);

        // Look for the end of HTTP headers (double CRLF)
        if (!reader.TryAdvanceTo((byte)'\n', advancePastDelimiter: false))
            return false;

        if (!reader.TryRead(out byte next) || next != (byte)'\r')
            return false;

        if (!reader.TryRead(out next) || next != (byte)'\n')
            return false;

        // Check if we have a Content-Length header to determine request body size
        // This is a simplified implementation - real implementation would need to parse headers

        position = reader.Position;
        return true;
    }

    /// <summary>
    /// Processes the HTTP request and generates an appropriate response
    /// </summary>
    /// <param name="requestData">HTTP request data</param>
    /// <param name="remoteEndPoint">Client endpoint information</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>HTTP response as a byte array</returns>
    private async ValueTask<byte[]> ProcessHttpRequestAsync(
        ReadOnlySequence<byte> requestData,
        IPEndPoint remoteEndPoint,
        CancellationToken ct
    )
    {
        // Parse the HTTP request (simplified)
        var requestText = GetRequestText(requestData);
        await _logger.InfoAsync(
            $"Received HTTP request: {requestText.Split('\n')[0]}",
            cancellationToken: ct
        );

        // Generate a simple HTTP response
        var responseBody = $"Hello from Pico.Node! Your IP: {remoteEndPoint.Address}\r\n";
        var response =
            $"HTTP/1.1 200 OK\r\n"
            + $"Content-Type: text/plain\r\n"
            + $"Content-Length: {responseBody.Length}\r\n"
            + $"Connection: keep-alive\r\n"
            + $"\r\n"
            + $"{responseBody}";

        return Encoding.UTF8.GetBytes(response);
    }

    /// <summary>
    /// Converts the request buffer to text for logging
    /// </summary>
    /// <param name="requestData">HTTP request data</param>
    /// <returns>String representation of the request</returns>
    private string GetRequestText(ReadOnlySequence<byte> requestData)
    {
        if (requestData.IsSingleSegment)
        {
            return Encoding.UTF8.GetString(requestData.First.Span);
        }

        // Multi-segment buffer
        var sb = new StringBuilder();
        foreach (var segment in requestData)
        {
            sb.Append(Encoding.UTF8.GetString(segment.Span));
        }
        return sb.ToString();
    }

    /// <summary>
    /// Writes an HTTP response to the PipeWriter
    /// </summary>
    /// <param name="writer">PipeWriter to write to</param>
    /// <param name="response">HTTP response bytes</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Task representing the write operation</returns>
    private async ValueTask WriteHttpResponseAsync(
        PipeWriter writer,
        byte[] response,
        CancellationToken ct
    )
    {
        var memory = writer.GetMemory(response.Length);
        response.CopyTo(memory);
        writer.Advance(response.Length);
        await writer.FlushAsync(ct);
    }

    /// <summary>
    /// Writes an error response when an exception occurs
    /// </summary>
    /// <param name="writer">PipeWriter to write to</param>
    /// <param name="exception">Exception that occurred</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Task representing the write operation</returns>
    private async ValueTask WriteErrorResponseAsync(
        PipeWriter writer,
        Exception exception,
        CancellationToken ct
    )
    {
        var errorMessage =
            $"HTTP/1.1 500 Internal Server Error\r\nContent-Type: text/plain\r\nConnection: close\r\n\r\nError: {exception.Message}";
        var errorBytes = Encoding.UTF8.GetBytes(errorMessage);

        var memory = writer.GetMemory(errorBytes.Length);
        errorBytes.CopyTo(memory);
        writer.Advance(errorBytes.Length);
        await writer.FlushAsync(ct);
    }

    /// <summary>
    /// Determines if the HTTP connection should be closed based on the request
    /// </summary>
    /// <param name="requestData">HTTP request data</param>
    /// <returns>True if the connection should be closed</returns>
    private bool ShouldCloseConnection(ReadOnlySequence<byte> requestData)
    {
        // Simplified implementation - real implementation would parse the Connection header
        var requestText = GetRequestText(requestData);
        return requestText.Contains("Connection: close") || requestText.StartsWith("HTTP/1.0");
    }
}
