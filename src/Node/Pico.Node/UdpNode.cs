namespace Pico.Node;

/// <summary>
/// UDP server node implementation using Channel for message processing
/// and ArrayPool for efficient buffer management
/// </summary>
public sealed class UdpNode : INode
{
    private readonly Socket _socket;
    private readonly IUdpHandler _handler;
    private readonly ILogger<UdpNode> _logger;
    private readonly Channel<UdpWorkItem> _channel;
    private readonly CancellationTokenSource _cts = new();
    private Task? _receiveTask;
    private Task? _processTask;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the UdpNode class
    /// </summary>
    /// <param name="endpoint">The endpoint to listen on</param>
    /// <param name="handler">The message handler</param>
    /// <param name="logger">Logger instance</param>
    /// <param name="maxQueueLength">Maximum queue length for processing</param>
    public UdpNode(
        IPEndPoint endpoint,
        IUdpHandler handler,
        ILogger<UdpNode> logger,
        int maxQueueLength = 10000
    )
    {
        _socket = new Socket(SocketType.Dgram, ProtocolType.Udp);
        _handler = handler;
        _logger = logger;

        // Create a bounded channel with backpressure
        var channelOptions = new BoundedChannelOptions(maxQueueLength)
        {
            FullMode = BoundedChannelFullMode.Wait, // Wait when queue is full
            SingleReader = false, // Allow multiple readers
            SingleWriter = true // Only one writer (the receive loop)
        };

        _channel = Channel.CreateBounded<UdpWorkItem>(channelOptions);

        // Configure socket options
        _socket.ReceiveBufferSize = 1024 * 1024; // 1MB receive buffer
        _socket.SendBufferSize = 1024 * 1024; // 1MB send buffer
        _socket.EnableBroadcast = true; // Allow sending broadcast messages

        _socket.Bind(endpoint);
    }

    /// <summary>
    /// Starts the UDP server
    /// </summary>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Task representing the server operation</returns>
    public async ValueTask StartAsync(CancellationToken ct = default)
    {
        await _logger.InfoAsync("Starting UDP server...", cancellationToken: ct);

        // Combine the provided token with our internal token
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, _cts.Token);
        var linkedToken = linkedCts.Token;

        await _logger.InfoAsync(
            $"UDP server started and listening on {_socket.LocalEndPoint}",
            cancellationToken: linkedToken
        );

        // Start the receive and processing tasks
        _receiveTask = ReceiveLoop(linkedToken);
        _processTask = ProcessUdpMessages(linkedToken);

        // Wait for both tasks to complete
        await Task.WhenAll(_receiveTask, _processTask);

        await _logger.InfoAsync("UDP server stopped", cancellationToken: linkedToken);
    }

    /// <summary>
    /// The main receive loop that reads datagrams from the socket
    /// </summary>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Task representing the receive operation</returns>
    private async Task ReceiveLoop(CancellationToken ct)
    {
        // Use a reusable endpoint object to avoid allocations
        EndPoint remoteEndPoint = new IPEndPoint(IPAddress.Any, 0);

        while (!ct.IsCancellationRequested)
        {
            // Rent a buffer from the ArrayPool
            var buffer = ArrayPool<byte>.Shared.Rent(65527); // Max UDP datagram size

            try
            {
                // Receive a datagram
                var result = await _socket.ReceiveFromAsync(
                    new ArraySegment<byte>(buffer),
                    SocketFlags.None,
                    remoteEndPoint,
                    ct
                );

                var sender = (IPEndPoint)result.RemoteEndPoint;

                // Create a work item with the received data
                var workItem = new UdpWorkItem(buffer, result.ReceivedBytes, sender);

                // Write the work item to the channel (with backpressure)
                await _channel.Writer.WriteAsync(workItem, ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                // Normal shutdown - return the buffer and break
                ArrayPool<byte>.Shared.Return(buffer);
                break;
            }
            catch (Exception ex)
            {
                // Return the buffer on error and log
                ArrayPool<byte>.Shared.Return(buffer);
                await _logger.ErrorAsync("Error receiving UDP datagram", ex, cancellationToken: ct);
            }
        }

        // Signal that no more items will be written to the channel
        _channel.Writer.Complete();
    }

    /// <summary>
    /// Processes UDP messages from the channel
    /// </summary>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Task representing the processing operation</returns>
    private async Task ProcessUdpMessages(CancellationToken ct)
    {
        try
        {
            await foreach (var workItem in _channel.Reader.ReadAllAsync(ct))
            {
                // Use a using statement to ensure the buffer is returned to the pool
                using (workItem)
                {
                    try
                    {
                        // Process the datagram
                        await _handler.HandleAsync(workItem.Datagram, workItem.RemoteEndPoint, ct);
                    }
                    catch (Exception ex)
                    {
                        await _logger.ErrorAsync(
                            $"Error processing UDP message from {workItem.RemoteEndPoint}",
                            ex,
                            cancellationToken: ct
                        );
                    }
                }
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // Normal shutdown
        }
        catch (Exception ex)
        {
            await _logger.ErrorAsync(
                "Error in UDP message processing loop",
                ex,
                cancellationToken: ct
            );
        }
    }

    /// <summary>
    /// Sends a datagram to a remote endpoint
    /// </summary>
    /// <param name="datagram">The datagram to send</param>
    /// <param name="remoteEndPoint">The remote endpoint</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Task representing the send operation</returns>
    public async Task SendAsync(
        ReadOnlyMemory<byte> datagram,
        IPEndPoint remoteEndPoint,
        CancellationToken ct = default
    )
    {
        try
        {
            await _socket.SendToAsync(datagram, SocketFlags.None, remoteEndPoint, ct);
        }
        catch (Exception ex)
        {
            await _logger.ErrorAsync(
                $"Error sending UDP datagram to {remoteEndPoint}",
                ex,
                cancellationToken: ct
            );
            throw;
        }
    }

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

        await CastAndDispose(_socket);
        await CastAndDispose(_cts);
        await CastAndDispose(_receiveTask);
        await CastAndDispose(_processTask);

        return;

        static async ValueTask CastAndDispose(IDisposable? resource)
        {
            switch (resource)
            {
                case null:
                    return;
                case IAsyncDisposable resourceAsyncDisposable:
                    await resourceAsyncDisposable.DisposeAsync();
                    break;
                default:
                    resource.Dispose();
                    break;
            }
        }
    }
}
