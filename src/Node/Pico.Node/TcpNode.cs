namespace Pico.Node;

public sealed class TcpNode : IAsyncDisposable
{
    // Dependencies and Configuration
    private readonly TcpNodeOptions _options;
    private readonly ILogger<TcpNode> _logger;
    private readonly Func<IPipelineHandler> _handlerFactory;
    private readonly Socket _serverSocket; // Using raw Socket for listening

    // State Management
    private readonly CancellationTokenSource _serverCts = new();
    private readonly Lock _stateLock = new(); // Standard lock object
    private bool _isRunning;
    private bool _isDisposed;

    // Core Architecture Components
    private Channel<Socket>? _connectionChannel; // Changed from TcpClient to Socket
    private Task? _acceptorTask;
    private readonly List<Task> _workerTasks = [];

    /// <summary>
    /// Initializes a new instance of the TcpNode class.
    /// </summary>
    /// <param name="options">Configuration options for the TCP node.</param>
    /// <param name="handlerFactory">A factory function to create IPipelineHandler instances for each connection.</param>
    /// <param name="logger">Logger instance for logging server events.</param>
    public TcpNode(
        TcpNodeOptions options,
        Func<IPipelineHandler> handlerFactory,
        ILogger<TcpNode> logger
    )
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _handlerFactory = handlerFactory ?? throw new ArgumentNullException(nameof(handlerFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        // Initialize the raw server socket, supporting IPv4 or IPv6 based on the IPAddress provided
        _serverSocket = new Socket(
            _options.IpAddress.AddressFamily, // Use AddressFamily from options for IPv4/IPv6 flexibility
            SocketType.Stream, // For TCP, we use Stream sockets
            ProtocolType.Tcp // Specify TCP protocol
        );

        // Enable port reuse for faster restarts.
        try
        {
            _serverSocket.SetSocketOption(
                SocketOptionLevel.Socket,
                SocketOptionName.ReuseAddress,
                true
            );
        }
        catch (Exception ex)
        {
            _logger.Warning("Setting ReuseAddress socket option failed.", ex);
        }
    }

    /// <summary>
    /// Starts the TCP server.
    /// </summary>
    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        lock (_stateLock)
        {
            if (_isRunning || _isDisposed)
            {
                return Task.CompletedTask;
            }

            try
            {
                // Create the bounded channel to hold accepted client sockets.
                _connectionChannel = Channel.CreateBounded<Socket>(
                    new BoundedChannelOptions(_options.ChannelCapacity)
                    {
                        FullMode = BoundedChannelFullMode.Wait, // Wait for space, creating natural backpressure.
                        SingleReader = false, // Multiple workers will read.
                        SingleWriter = true // The acceptor is the single writer.
                    }
                );

                // Bind the socket to the specified IP address and port
                _serverSocket.Bind(new IPEndPoint(_options.IpAddress, _options.Port));
                // Start listening for incoming connection requests with a specified backlog
                _serverSocket.Listen(_options.ListenBacklog);

                _isRunning = true;

                // Link the external cancellation token with the internal one.
                var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
                    cancellationToken,
                    _serverCts.Token
                );

                // Start the producer (acceptor) and consumer (worker) tasks.
                _acceptorTask = RunAcceptorAsync(linkedCts.Token);
                for (var i = 0; i < _options.MaxConcurrentConnections; i++)
                {
                    _workerTasks.Add(RunWorkerAsync(i, linkedCts.Token));
                }

                // Log detailed startup information
                _logger.Info(
                    $"TCP server started on {_options.IpAddress}:{_options.Port} (Max connections: {_options.MaxConcurrentConnections}, Channel capacity: {_options.ChannelCapacity}, Listen backlog: {_options.ListenBacklog})."
                );
            }
            catch (Exception ex)
            {
                _logger.Critical("Failed to start TCP server.", ex);
                StopServer(); // Attempt to clean up.
                throw;
            }
        }
        return Task.CompletedTask;
    }

    /// <summary>
    /// The "Producer" task. Its sole responsibility is to accept incoming connections
    /// and queue them for processing.
    /// </summary>
    private async Task RunAcceptorAsync(CancellationToken cancellationToken)
    {
        await _logger.InfoAsync(
            $"TCP Acceptor started on {_options.IpAddress}:{_options.Port}.",
            cancellationToken
        );
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                // Asynchronously accept a client socket.
                // If the channel is full, the subsequent WriteAsync will pause, providing backpressure.
                var acceptedSocket = await _serverSocket.AcceptAsync(cancellationToken);

                // Enqueue the raw socket directly for a worker to process.
                await _connectionChannel!.Writer.WriteAsync(acceptedSocket, cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
            // Expected when the server is stopping due to cancellation token.
        }
        catch (SocketException sex) when (sex.SocketErrorCode == SocketError.OperationAborted)
        {
            // Expected when the listening socket is closed (e.g., by StopServer)
        }
        catch (Exception ex)
        {
            await _logger.CriticalAsync(
                "Acceptor loop encountered a fatal error.",
                ex,
                cancellationToken
            );
        }
        finally
        {
            // Mark the channel as complete, signaling to workers that no more items will be added.
            _connectionChannel?.Writer.Complete();
            await _logger.InfoAsync("TCP Acceptor stopped.", cancellationToken);
        }
    }

    /// <summary>
    /// The "Consumer" task. A pool of these workers will process connections from the channel.
    /// </summary>
    private async Task RunWorkerAsync(int workerId, CancellationToken cancellationToken)
    {
        await _logger.InfoAsync($"Worker #{workerId} started.", cancellationToken);
        try
        {
            // This will loop until the channel is marked as complete and is empty.
            await foreach (var socket in _connectionChannel!.Reader.ReadAllAsync(cancellationToken))
            {
                // Process one client connection. A try/catch ensures that one failed client
                // does not terminate the entire worker.
                try
                {
                    await ProcessConnectionAsync(socket, cancellationToken);
                }
                catch (Exception ex)
                {
                    var remoteEndPoint = SafeGetIpEndPoint(socket);
                    await _logger.ErrorAsync(
                        $"Unhandled exception in connection processing for '{remoteEndPoint}'. Worker continues.",
                        ex,
                        cancellationToken
                    );
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Expected when the server is stopping.
        }
        catch (Exception ex)
        {
            await _logger.CriticalAsync(
                $"Worker #{workerId} encountered a fatal error.",
                ex,
                cancellationToken
            );
        }
        finally
        {
            await _logger.InfoAsync($"Worker #{workerId} stopped.", cancellationToken);
        }
    }

    /// <summary>
    /// Handles the lifecycle of a single client connection using System.IO.Pipelines.
    /// </summary>
    private async Task ProcessConnectionAsync(Socket socket, CancellationToken cancellationToken)
    {
        using (socket) // Ensure the socket is disposed when we are done.
        {
            var remoteEndPoint = SafeGetIpEndPoint(socket);
            await _logger.DebugAsync(
                $"Processing connection from: {remoteEndPoint}",
                cancellationToken
            );

            // Create a NetworkStream from the socket.
            // We specify `ownsSocket: false` because the `using(socket)` block is already responsible for its disposal.
            await using var stream = new NetworkStream(socket, ownsSocket: false);

            // Create PipeReader and PipeWriter from the NetworkStream
            var reader = PipeReader.Create(stream);
            var writer = PipeWriter.Create(stream);

            IPipelineHandler? handler = null;
            try
            {
                handler = _handlerFactory();
                await handler.HandleAsync(reader, writer, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                // Normal cancellation during graceful shutdown.
            }
            catch (Exception ex)
            {
                await _logger.ErrorAsync(
                    $"Error processing connection from {remoteEndPoint}.",
                    ex,
                    cancellationToken
                );
                _options.ExceptionHandler?.Invoke(ex, remoteEndPoint);
            }
            finally
            {
                // Gracefully complete the pipelines and dispose the handler if applicable.
                await reader.CompleteAsync();
                await writer.CompleteAsync();

                switch (handler)
                {
                    case IAsyncDisposable asyncDisposable:
                        await asyncDisposable.DisposeAsync();
                        break;
                    case IDisposable disposable:
                        disposable.Dispose();
                        break;
                }
                await _logger.DebugAsync(
                    $"Finished connection from: {remoteEndPoint}",
                    cancellationToken
                );
            }
        }
    }

    /// <summary>
    /// Stops the server gracefully.
    /// </summary>
    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        lock (_stateLock)
        {
            if (!_isRunning)
            {
                return;
            }
            StopServer(); // Initiate cancellation and stop listening
        }

        var allTasks = new List<Task>();
        if (_acceptorTask != null)
            allTasks.Add(_acceptorTask);
        allTasks.AddRange(_workerTasks);

        try
        {
            // Wait for all tasks to complete within the specified timeout.
            using var timeoutCts = new CancellationTokenSource(_options.StopTimeout);
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken,
                timeoutCts.Token
            );
            await Task.WhenAll(allTasks).WaitAsync(linkedCts.Token);
            await _logger.InfoAsync("Server stopped gracefully.", linkedCts.Token);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            await _logger.WarningAsync(
                "Server shutdown was cancelled by the caller.",
                cancellationToken: cancellationToken
            );
        }
        catch (Exception ex) // Catches TimeoutException from WaitAsync and others.
        {
            await _logger.WarningAsync(
                $"Graceful shutdown timed out or failed after {_options.StopTimeout.TotalSeconds} seconds.",
                ex,
                cancellationToken
            );
        }
    }

    /// <summary>
    /// Private method to perform the non-async part of server shutdown.
    /// </summary>
    private void StopServer()
    {
        lock (_stateLock)
        {
            if (!_isRunning)
                return;

            _isRunning = false;
            _serverCts.Cancel(); // Signal all tasks to cancel

            try
            {
                // Close the server socket to stop accepting new connections
                _serverSocket.Close();
            }
            catch (Exception ex)
            {
                _logger.Warning("Error closing server socket.", ex);
            }
        }
    }

    /// <summary>
    /// Disposes all resources used by the server.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        if (_isDisposed)
        {
            return;
        }
        _isDisposed = true;

        // Ensure the server is stopped before disposing resources
        await StopAsync(CancellationToken.None);

        _serverCts.Dispose();
        _serverSocket.Dispose(); // Dispose the raw socket
    }

    /// <summary>
    /// Safely retrieves the IPEndPoint of a Socket.
    /// </summary>
    /// <param name="socket">The Socket instance.</param>
    /// <returns>The IPEndPoint or null if it cannot be retrieved.</returns>
    private static IPEndPoint? SafeGetIpEndPoint(Socket socket)
    {
        try
        {
            return socket.RemoteEndPoint as IPEndPoint;
        }
        catch
        {
            // Can throw if the socket is already closed or disposed.
            return null;
        }
    }
}
