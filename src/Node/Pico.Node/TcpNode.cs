namespace Pico.Node;

/// <summary>
/// High-performance TCP server node implementation
/// </summary>
public sealed class TcpNode : INode
{
    private readonly Socket _listenerSocket;
    private readonly ITcpHandler _handler;
    private readonly IPEndPoint _localEndPoint;
    private readonly int _backlog;
    private readonly TimeSpan _connectionTimeout;
    private readonly TimeSpan _heartbeatInterval;
    private readonly CancellationTokenSource _cts = new();
    private readonly ConcurrentDictionary<Guid, TcpClientConnection> _connections = new();
    private readonly ILogger<TcpNode> _logger;
    private bool _disposed;
    private bool _listening;
    private Task _acceptClientsTask = Task.CompletedTask;

    /// <summary>
    /// Event raised when a client connection is established
    /// </summary>
    public event EventHandler<ConnectionEventArgs>? ClientConnected;

    /// <summary>
    /// Event raised when a client connection is terminated
    /// </summary>
    public event EventHandler<ConnectionEventArgs>? ClientDisconnected;

    /// <summary>
    /// Event raised when an error occurs in the node
    /// </summary>
    public event EventHandler<ErrorEventArgs>? ErrorOccurred;

    /// <summary>
    /// Creates a high-performance TCP node
    /// </summary>
    /// <param name="handler">The TCP handler for processing connections</param>
    /// <param name="localEndPoint">The local endpoint to bind to</param>
    /// <param name="logger">Logger instance for diagnostics</param>
    /// <param name="backlog">Maximum pending connections queue size</param>
    /// <param name="connectionTimeout">Timeout for idle connections</param>
    /// <param name="heartbeatInterval">Interval for connection heartbeats</param>
    /// <param name="addressFamily">Address family for the socket</param>
    public TcpNode(
        ITcpHandler handler,
        IPEndPoint localEndPoint,
        ILogger<TcpNode>? logger = null,
        int backlog = 100,
        TimeSpan? connectionTimeout = null,
        TimeSpan? heartbeatInterval = null,
        AddressFamily addressFamily = AddressFamily.InterNetwork
    )
    {
        _handler = handler ?? throw new ArgumentNullException(nameof(handler));
        _localEndPoint = localEndPoint;
        _backlog = backlog;
        _connectionTimeout = connectionTimeout ?? TimeSpan.FromMinutes(5);
        _heartbeatInterval = heartbeatInterval ?? TimeSpan.FromSeconds(30);
        _logger = logger ?? new NullLogger<TcpNode>();

        // Create and configure listener socket
        _listenerSocket = new Socket(addressFamily, SocketType.Stream, ProtocolType.Tcp);

        // Configure high-performance options
        _listenerSocket.NoDelay = true; // Disable Nagle's algorithm
        _listenerSocket.LingerState = new LingerOption(false, 0); // Disable lingering close

        // Enable socket reuse
        _listenerSocket.SetSocketOption(
            SocketOptionLevel.Socket,
            SocketOptionName.ReuseAddress,
            true
        );
    }

    /// <summary>
    /// Asynchronously starts the TCP node
    /// </summary>
    /// <param name="cancellationToken">Cancellation token to stop the operation</param>
    /// <returns>A ValueTask representing the asynchronous operation</returns>
    public async ValueTask StartAsync(CancellationToken cancellationToken = default)
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(TcpNode));

        if (_listening)
            throw new InvalidOperationException("Node is already listening");

        try
        {
            _listenerSocket.Bind(_localEndPoint);
            _listenerSocket.Listen(_backlog);
            _listening = true;

            // Start accepting client connections
            _acceptClientsTask = Task.Run(() => AcceptClientsAsync(_cts.Token), _cts.Token);

            await _logger.InfoAsync(
                $"High-performance TCP server started on {_localEndPoint}",
                cancellationToken: cancellationToken
            );
        }
        catch (Exception ex)
        {
            await _logger.ErrorAsync(
                $"Error starting TCP server on {_localEndPoint}",
                ex,
                cancellationToken: cancellationToken
            );
            OnErrorOccurred(ex, "StartFailed");
            throw new NodeStartException($"Failed to start TCP server on {_localEndPoint}", ex);
        }
    }

    /// <summary>
    /// Asynchronously stops the TCP node
    /// </summary>
    /// <param name="cancellationToken">Cancellation token to stop the operation</param>
    /// <returns>A ValueTask representing the asynchronous operation</returns>
    public async ValueTask StopAsync(CancellationToken cancellationToken = default)
    {
        if (!_listening)
            return;

        _listening = false;
        await _cts.CancelAsync();

        try
        {
            // Wait for accept loop to complete
            await _acceptClientsTask.WaitAsync(TimeSpan.FromSeconds(5), cancellationToken);
        }
        catch (OperationCanceledException)
        {
            // Expected during shutdown
        }
        catch (TimeoutException ex)
        {
            await _logger.WarningAsync(
                "Timeout waiting for accept loop to complete",
                ex,
                cancellationToken: cancellationToken
            );
        }

        // Close all active connections
        var closeTasks = _connections
            .Values
            .Select(connection => CloseConnectionAsync(connection, "ServerShutdown"))
            .ToArray();

        // Wait for all connections to close with timeout
        try
        {
            await Task.WhenAll(closeTasks).WaitAsync(TimeSpan.FromSeconds(10), cancellationToken);
        }
        catch (TimeoutException ex)
        {
            await _logger.WarningAsync(
                "Timeout waiting for connections to close during shutdown",
                ex,
                cancellationToken: cancellationToken
            );
        }

        // Clear connections collection
        _connections.Clear();

        // Close listener socket
        try
        {
            _listenerSocket.Close();
            _listenerSocket.Dispose();
        }
        catch (Exception ex)
        {
            await _logger.WarningAsync(
                "Error closing listener socket",
                ex,
                cancellationToken: cancellationToken
            );
        }

        await _logger.InfoAsync(
            $"TCP server stopped on {_localEndPoint}",
            cancellationToken: cancellationToken
        );
    }

    /// <summary>
    /// Asynchronously accepts client connections
    /// </summary>
    /// <param name="cancellationToken">Cancellation token to stop the operation</param>
    private async Task AcceptClientsAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var clientSocket = await _listenerSocket.AcceptAsync(cancellationToken);

                // Configure client socket for high performance
                clientSocket.NoDelay = true;
                clientSocket.LingerState = new LingerOption(false, 0);

                // Create and track client connection
                var connection = new TcpClientConnection(
                    clientSocket,
                    _connectionTimeout,
                    _heartbeatInterval
                );
                if (_connections.TryAdd(connection.Id, connection))
                {
                    OnClientConnected(connection);

                    // Process connection in background with timeout
                    _ = ProcessConnectionWithTimeoutAsync(connection, cancellationToken);
                }
                else
                {
                    await _logger.WarningAsync(
                        $"Failed to add connection {connection.Id} to tracking dictionary",
                        cancellationToken: cancellationToken
                    );
                    await CloseConnectionAsync(connection, "TrackingError");
                }
            }
            catch (OperationCanceledException)
            {
                // Normal cancellation during shutdown
                break;
            }
            catch (SocketException ex) when (ex.SocketErrorCode == SocketError.OperationAborted)
            {
                // Socket closed during shutdown
                break;
            }
            catch (Exception ex)
            {
                await _logger.ErrorAsync(
                    "Error accepting client connection",
                    ex,
                    cancellationToken: cancellationToken
                );
                OnErrorOccurred(ex, "AcceptError");

                // Brief delay before accepting again to prevent tight error loops
                await Task.Delay(100, cancellationToken);
            }
        }
    }

    /// <summary>
    /// Processes a connection with timeout protection
    /// </summary>
    /// <param name="connection">The client connection to process</param>
    /// <param name="cancellationToken">Cancellation token to stop the operation</param>
    private async Task ProcessConnectionWithTimeoutAsync(
        TcpClientConnection connection,
        CancellationToken cancellationToken
    )
    {
        using var timeoutCts = new CancellationTokenSource();
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken,
            timeoutCts.Token
        );

        try
        {
            // Set up timeout for connection processing
            timeoutCts.CancelAfter(_connectionTimeout);

            await ProcessConnectionAsync(connection, linkedCts.Token);
        }
        catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested)
        {
            await _logger.WarningAsync(
                $"Connection {connection.Id} timed out after {_connectionTimeout}",
                cancellationToken: linkedCts.Token
            );
            await CloseConnectionAsync(connection, "Timeout");
        }
        catch (Exception ex)
        {
            await _logger.ErrorAsync(
                $"Error processing connection {connection.Id}",
                ex,
                cancellationToken: linkedCts.Token
            );
            await CloseConnectionAsync(connection, "ProcessingError");
        }
    }

    /// <summary>
    /// Processes a client connection using System.IO.Pipelines
    /// </summary>
    /// <param name="connection">The client connection to process</param>
    /// <param name="cancellationToken">Cancellation token to stop the operation</param>
    private async Task ProcessConnectionAsync(
        TcpClientConnection connection,
        CancellationToken cancellationToken
    )
    {
        // Create Pipe for efficient reading
        var pipe = new Pipe();
        var writing = FillPipeAsync(connection.Socket, pipe.Writer, cancellationToken);
        var reading = ReadPipeAsync(pipe.Reader, connection, cancellationToken);

        // Wait for both reading and writing tasks to complete
        await Task.WhenAll(reading, writing);

        // Clean up connection
        _connections.TryRemove(connection.Id, out _);
        OnClientDisconnected(connection);
        connection.Dispose();
    }

    /// <summary>
    /// Reads data from socket and writes to Pipe
    /// </summary>
    /// <param name="socket">The socket to read from</param>
    /// <param name="writer">The pipe writer to write to</param>
    /// <param name="cancellationToken">Cancellation token to stop the operation</param>
    private static async Task FillPipeAsync(
        Socket socket,
        PipeWriter writer,
        CancellationToken cancellationToken
    )
    {
        const int minimumBufferSize = 4096;

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                // Get memory from PipeWriter
                var memory = writer.GetMemory(minimumBufferSize);

                // Read data from socket
                var bytesRead = await socket.ReceiveAsync(
                    memory,
                    SocketFlags.None,
                    cancellationToken
                );

                if (bytesRead == 0)
                {
                    break; // Connection closed
                }

                // Tell PipeWriter how much data we've written
                writer.Advance(bytesRead);

                // Make data available to PipeReader
                var result = await writer.FlushAsync(cancellationToken);

                if (result.IsCompleted)
                {
                    break;
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (SocketException ex)
                when (ex.SocketErrorCode
                        is SocketError.ConnectionReset
                            or SocketError.ConnectionAborted
                )
            {
                break; // Connection closed by client
            }
            catch (Exception ex)
            {
                // Logging would happen at higher level
                break;
            }
        }

        // Tell PipeReader there's no more data
        await writer.CompleteAsync();
    }

    /// <summary>
    /// Reads data from Pipe and processes it using the handler
    /// </summary>
    /// <param name="reader">The pipe reader to read from</param>
    /// <param name="connection">The client connection</param>
    /// <param name="cancellationToken">Cancellation token to stop the operation</param>
    private async Task ReadPipeAsync(
        PipeReader reader,
        TcpClientConnection connection,
        CancellationToken cancellationToken
    )
    {
        try
        {
            // Create a PipeWriter directly from the socket for efficient writing
            var writer = PipeWriter.Create(connection.Socket);

            // Start heartbeat monitoring
            using var heartbeat = StartHeartbeat(connection, writer, cancellationToken);

            // Call handler to process connection
            await _handler.HandleAsync(
                reader,
                writer,
                connection.RemoteEndPoint,
                cancellationToken
            );
        }
        catch (OperationCanceledException)
        {
            // Expected during cancellation
        }
        catch (Exception ex)
        {
            await _logger.ErrorAsync(
                $"Error in TCP handler for connection {connection.Id}",
                ex,
                cancellationToken: cancellationToken
            );
            OnErrorOccurred(ex, "HandlerError");
            throw;
        }
        finally
        {
            // Complete reading
            await reader.CompleteAsync();
        }
    }

    /// <summary>
    /// Starts heartbeat monitoring for a connection
    /// </summary>
    /// <param name="connection">The connection to monitor</param>
    /// <param name="writer">The writer for sending heartbeats</param>
    /// <param name="cancellationToken">Cancellation token to stop the operation</param>
    /// <returns>An IDisposable to stop heartbeats</returns>
    private IDisposable StartHeartbeat(
        TcpClientConnection connection,
        PipeWriter writer,
        CancellationToken cancellationToken
    )
    {
        var cts = new CancellationTokenSource();
        var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
            cts.Token,
            cancellationToken
        );

        // Start heartbeat task
        var heartbeatTask = Task.Run(
            async () =>
            {
                while (!linkedCts.Token.IsCancellationRequested)
                {
                    try
                    {
                        await Task.Delay(_heartbeatInterval, linkedCts.Token);

                        if (connection.IsAlive)
                        {
                            // Reset alive flag - handler should set it on activity
                            connection.IsAlive = false;
                        }
                        else
                        {
                            await _logger.WarningAsync(
                                $"Connection {connection.Id} failed heartbeat",
                                cancellationToken: linkedCts.Token
                            );
                            await CloseConnectionAsync(connection, "HeartbeatFailed");
                            break;
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                }
            },
            linkedCts.Token
        );

        return new DisposableAction(() =>
        {
            cts.Cancel();
            heartbeatTask.ContinueWith(
                _ =>
                { /* Ignore task exceptions */
                },
                linkedCts.Token
            );
        });
    }

    /// <summary>
    /// Closes a connection and removes it from tracking
    /// </summary>
    /// <param name="connection">The connection to close</param>
    /// <param name="reason">The reason for closing</param>
    private async Task CloseConnectionAsync(TcpClientConnection connection, string reason)
    {
        try
        {
            _connections.TryRemove(connection.Id, out _);
            OnClientDisconnected(connection, reason);

            // Shutdown socket gracefully
            try
            {
                connection.Socket.Shutdown(SocketShutdown.Both);
            }
            catch (SocketException ex)
            {
                await _logger.DebugAsync(
                    $"Error shutting down socket for connection {connection.Id}",
                    ex
                );
            }

            connection.Dispose();
        }
        catch (Exception ex)
        {
            await _logger.ErrorAsync($"Error closing connection {connection.Id}", ex);
        }
    }

    /// <summary>
    /// Raises the ClientConnected event
    /// </summary>
    /// <param name="connection">The connected client</param>
    private void OnClientConnected(TcpClientConnection connection)
    {
        ClientConnected?.Invoke(
            this,
            new ConnectionEventArgs(connection.Id, connection.RemoteEndPoint)
        );
    }

    /// <summary>
    /// Raises the ClientDisconnected event
    /// </summary>
    /// <param name="connection">The disconnected client</param>
    /// <param name="reason">The reason for disconnection</param>
    private void OnClientDisconnected(TcpClientConnection connection, string reason = "Normal")
    {
        ClientDisconnected?.Invoke(
            this,
            new ConnectionEventArgs(connection.Id, connection.RemoteEndPoint, reason)
        );
    }

    /// <summary>
    /// Raises the ErrorOccurred event
    /// </summary>
    /// <param name="exception">The exception that occurred</param>
    /// <param name="context">The context where the error occurred</param>
    private void OnErrorOccurred(Exception exception, string context)
    {
        ErrorOccurred?.Invoke(this, new ErrorEventArgs(exception, context));
    }

    /// <summary>
    /// Releases resources asynchronously
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;

        _disposed = true;
        await StopAsync();
        _cts.Dispose();
        _listenerSocket.Dispose();
        GC.SuppressFinalize(this);
    }
}

/// <summary>
/// Represents a TCP client connection
/// </summary>
public class TcpClientConnection : IDisposable
{
    private bool _disposed;

    /// <summary>
    /// Gets the unique connection identifier
    /// </summary>
    public Guid Id { get; } = Guid.NewGuid();

    /// <summary>
    /// Gets the client socket
    /// </summary>
    public Socket Socket { get; }

    /// <summary>
    /// Gets the remote endpoint
    /// </summary>
    public IPEndPoint RemoteEndPoint { get; }

    /// <summary>
    /// Gets or sets a value indicating if the connection is alive
    /// </summary>
    public bool IsAlive { get; set; } = true;

    /// <summary>
    /// Gets the connection timeout
    /// </summary>
    public TimeSpan Timeout { get; }

    /// <summary>
    /// Gets the heartbeat interval
    /// </summary>
    public TimeSpan HeartbeatInterval { get; }

    /// <summary>
    /// Creates a new TCP client connection
    /// </summary>
    /// <param name="socket">The client socket</param>
    /// <param name="timeout">The connection timeout</param>
    /// <param name="heartbeatInterval">The heartbeat interval</param>
    public TcpClientConnection(Socket socket, TimeSpan timeout, TimeSpan heartbeatInterval)
    {
        Socket = socket ?? throw new ArgumentNullException(nameof(socket));
        RemoteEndPoint = (IPEndPoint)socket.RemoteEndPoint!;
        Timeout = timeout;
        HeartbeatInterval = heartbeatInterval;
    }

    /// <summary>
    /// Disposes the connection
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;

        try
        {
            Socket.Close();
            Socket.Dispose();
        }
        catch
        {
            // Ignore errors during disposal
        }
    }
}

/// <summary>
/// Event arguments for connection events
/// </summary>
public class ConnectionEventArgs : EventArgs
{
    /// <summary>
    /// Gets the connection ID
    /// </summary>
    public Guid ConnectionId { get; }

    /// <summary>
    /// Gets the remote endpoint
    /// </summary>
    public IPEndPoint RemoteEndPoint { get; }

    /// <summary>
    /// Gets the reason for disconnection (if applicable)
    /// </summary>
    public string? Reason { get; }

    /// <summary>
    /// Creates new connection event arguments
    /// </summary>
    /// <param name="connectionId">The connection ID</param>
    /// <param name="remoteEndPoint">The remote endpoint</param>
    /// <param name="reason">The reason for disconnection</param>
    public ConnectionEventArgs(Guid connectionId, IPEndPoint remoteEndPoint, string? reason = null)
    {
        ConnectionId = connectionId;
        RemoteEndPoint = remoteEndPoint;
        Reason = reason;
    }
}

/// <summary>
/// Event arguments for error events
/// </summary>
public class ErrorEventArgs : EventArgs
{
    /// <summary>
    /// Gets the exception that occurred
    /// </summary>
    public Exception Exception { get; }

    /// <summary>
    /// Gets the context where the error occurred
    /// </summary>
    public string Context { get; }

    /// <summary>
    /// Creates new error event arguments
    /// </summary>
    /// <param name="exception">The exception that occurred</param>
    /// <param name="context">The context where the error occurred</param>
    public ErrorEventArgs(Exception exception, string context)
    {
        Exception = exception;
        Context = context;
    }
}

/// <summary>
/// Exception thrown when node startup fails
/// </summary>
public class NodeStartException : Exception
{
    /// <summary>
    /// Creates a new node start exception
    /// </summary>
    /// <param name="message">The error message</param>
    /// <param name="innerException">The inner exception</param>
    public NodeStartException(string message, Exception innerException)
        : base(message, innerException) { }
}

/// <summary>
/// Simple disposable action helper
/// </summary>
internal class DisposableAction : IDisposable
{
    private readonly Action _action;

    /// <summary>
    /// Creates a new disposable action
    /// </summary>
    /// <param name="action">The action to execute on disposal</param>
    public DisposableAction(Action action)
    {
        _action = action ?? throw new ArgumentNullException(nameof(action));
    }

    /// <summary>
    /// Executes the action
    /// </summary>
    public void Dispose()
    {
        _action();
    }
}
