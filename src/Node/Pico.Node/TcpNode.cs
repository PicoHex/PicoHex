namespace Pico.Node;

/// <summary>
/// TCP server node implementation using Pipe for stream processing
/// and SemaphoreSlim for connection limiting
/// </summary>
public sealed class TcpNode : INode
{
    private readonly Socket _listenerSocket;
    private readonly Func<ITcpHandler> _handlerFactory;
    private readonly ILogger<TcpNode> _logger;
    private readonly SemaphoreSlim _connectionLimiter;
    private readonly int _backlogSize;
    private readonly CancellationTokenSource _cts = new();
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the TcpNode class
    /// </summary>
    public TcpNode(TcpNodeOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (options.Endpoint == null)
            throw new ArgumentException("Endpoint is required", nameof(options));

        _handlerFactory =
            options.HandlerFactory
            ?? throw new ArgumentException("Handler is required", nameof(options));
        _listenerSocket = new Socket(SocketType.Stream, ProtocolType.Tcp);
        _logger = options.Logger;
        _connectionLimiter = new SemaphoreSlim(options.MaxConnections, options.MaxConnections);
        _backlogSize = options.BacklogSize;

        // Configure socket options
        _listenerSocket.NoDelay = options.NoDelay;
        _listenerSocket.LingerState = options.LingerState;
        _listenerSocket.Bind(options.Endpoint);
    }

    /// <summary>
    /// Starts the TCP server
    /// </summary>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Task representing the server operation</returns>
    public async ValueTask StartAsync(CancellationToken ct = default)
    {
        await _logger.InfoAsync("Starting TCP server...", cancellationToken: ct);

        // Combine the provided token with our internal token
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, _cts.Token);
        var linkedToken = linkedCts.Token;

        _listenerSocket.Listen(_backlogSize);

        await _logger.InfoAsync(
            $"TCP server started and listening on {_listenerSocket.LocalEndPoint}",
            cancellationToken: linkedToken
        );

        try
        {
            // Main accept loop
            while (!linkedToken.IsCancellationRequested)
            {
                try
                {
                    // Wait for a connection slot if we're at the limit
                    await _connectionLimiter.WaitAsync(linkedToken);

                    // Accept the next connection
                    var clientSocket = await _listenerSocket.AcceptAsync(linkedToken);

                    // Process the connection without awaiting (fire and forget)
                    _ = ProcessClientAsync(clientSocket, linkedToken)
                        .ContinueWith(
                            async t =>
                            {
                                // Ensure the semaphore is released even if the task fails
                                _connectionLimiter.Release();

                                if (t.IsFaulted)
                                {
                                    await _logger.ErrorAsync(
                                        "Error processing client connection",
                                        t.Exception,
                                        cancellationToken: linkedToken
                                    );
                                }
                            },
                            TaskScheduler.Default
                        );
                }
                catch (OperationCanceledException) when (linkedToken.IsCancellationRequested)
                {
                    // Normal shutdown
                    break;
                }
                catch (Exception ex)
                {
                    await _logger.ErrorAsync(
                        "Error accepting TCP connection",
                        ex,
                        cancellationToken: linkedToken
                    );
                    await Task.Delay(1000, linkedToken); // Avoid tight loop on errors
                }
            }
        }
        finally
        {
            await _logger.InfoAsync("TCP server stopped", cancellationToken: linkedToken);
        }
    }

    /// <summary>
    /// Processes an individual client connection using Pipe for efficient stream processing
    /// </summary>
    /// <param name="clientSocket">The client socket</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Task representing the client processing operation</returns>
    private async Task ProcessClientAsync(Socket clientSocket, CancellationToken ct)
    {
        var remoteEndPoint = (IPEndPoint)clientSocket.RemoteEndPoint!;
        await _logger.InfoAsync($"Client connected from {remoteEndPoint}", cancellationToken: ct);

        // Create a pipe for efficient stream processing
        var pipe = new Pipe();

        // Start both reading from socket and processing through the handler
        var writing = FillPipeAsync(clientSocket, pipe.Writer, ct);
        var reading = ProcessWithHandlerAsync(pipe.Reader, pipe.Writer, remoteEndPoint, ct);

        // Wait for both tasks to complete
        await Task.WhenAll(writing, reading);

        // Clean up the socket
        try
        {
            clientSocket.Shutdown(SocketShutdown.Both);
            clientSocket.Close();
        }
        catch (Exception ex)
        {
            await _logger.WarningAsync("Error closing client socket", ex, cancellationToken: ct);
        }

        await _logger.InfoAsync(
            $"Client disconnected from {remoteEndPoint}",
            cancellationToken: ct
        );
    }

    /// <summary>
    /// Fills the pipe with data from the socket
    /// </summary>
    /// <param name="socket">The source socket</param>
    /// <param name="writer">The pipe writer</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Task representing the fill operation</returns>
    private async Task FillPipeAsync(Socket socket, PipeWriter writer, CancellationToken ct)
    {
        const int minimumBufferSize = 512;

        try
        {
            while (!ct.IsCancellationRequested)
            {
                // Allocate memory from the PipeWriter
                var memory = writer.GetMemory(minimumBufferSize);

                // Read data from the socket
                var bytesRead = await socket.ReceiveAsync(memory, SocketFlags.None, ct);
                if (bytesRead == 0)
                {
                    break; // Connection closed
                }

                // Tell the PipeWriter how much was read
                writer.Advance(bytesRead);

                // Make the data available to the PipeReader
                var result = await writer.FlushAsync(ct);
                if (result.IsCompleted)
                {
                    break;
                }
            }
        }
        catch (Exception ex)
        {
            await _logger.ErrorAsync("Error reading from socket", ex, cancellationToken: ct);
        }
        finally
        {
            // Tell the PipeReader that we're done writing
            await writer.CompleteAsync();
        }
    }

    /// <summary>
    /// Processes data using the TCP handler with both read and write capabilities
    /// </summary>
    /// <param name="reader">The pipe reader</param>
    /// <param name="writer">The pipe writer</param>
    /// <param name="remoteEndPoint">The remote endpoint</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Task representing the processing operation</returns>
    private async Task ProcessWithHandlerAsync(
        PipeReader reader,
        PipeWriter writer,
        IPEndPoint remoteEndPoint,
        CancellationToken ct
    )
    {
        try
        {
            // Pass both reader and writer to the handler for bidirectional communication
            await _handlerFactory().HandleAsync(reader, writer, remoteEndPoint, ct);
        }
        catch (Exception ex)
        {
            await _logger.ErrorAsync("Error in TCP handler", ex, cancellationToken: ct);
        }
        finally
        {
            // Mark both reader and writer as complete
            await reader.CompleteAsync();
            await writer.CompleteAsync();
        }
    }

    /// <summary>
    /// Stops the TCP server
    /// </summary>
    public ValueTask StopAsync(CancellationToken cancellationToken = default)
    {
        _cts.Cancel();
        return ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;
        _disposed = true;

        await StopAsync();

        // Dispose resources
        _listenerSocket.Dispose();
        _connectionLimiter.Dispose();
        _cts.Dispose();

        await Task.CompletedTask;
    }
}
