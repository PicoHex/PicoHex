namespace Pico.Node;

public sealed class TcpNodeV3 : INode
{
    // Dependencies and Configuration
    private readonly TcpNodeOptionsV3 _options;
    private readonly ILogger<TcpNodeV3> _logger;
    private readonly Func<IPipelineHandler> _handlerFactory;
    private readonly Socket? _listenSocket;

    // State Management
    private readonly CancellationTokenSource _serverCts = new();
    private readonly Lock _stateLock = new(); // Assuming Lock is a simple object for locking
    private bool _isRunning;
    private bool _isDisposed;

    // Core Architecture Components
    private Channel<TcpClient>? _connectionChannel;
    private Task? _acceptorTask;
    private readonly List<Task> _workerTasks = [];

    public TcpNodeV3(
        TcpNodeOptionsV3 options, // Use V3 options
        Func<IPipelineHandler> handlerFactory,
        ILogger<TcpNodeV3> logger
    )
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _handlerFactory = handlerFactory ?? throw new ArgumentNullException(nameof(handlerFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        // Initialize the listening socket
        _listenSocket = new Socket(
            _options.IpAddress.AddressFamily, // Use IPv4 or IPv6 based on the IPAddress provided
            SocketType.Stream, // For TCP, we use Stream sockets
            ProtocolType.Tcp // Specify TCP protocol
        );

        // Enable port reuse for faster restarts, a professional touch.
        // This option must be set before binding the socket.
        try
        {
            _listenSocket.SetSocketOption(
                SocketOptionLevel.Socket,
                SocketOptionName.ReuseAddress,
                true
            );
        }
        catch (Exception ex)
        {
            _logger
                .WarningAsync("Setting ReuseAddress socket option failed.", ex)
                .ConfigureAwait(false);
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
                // Create the bounded channel to hold accepted client connections.
                _connectionChannel = Channel.CreateBounded<TcpClient>(
                    new BoundedChannelOptions(_options.ChannelCapacity)
                    {
                        FullMode = BoundedChannelFullMode.Wait, // Wait for space, creating natural backpressure.
                        SingleReader = false, // Multiple workers will read.
                        SingleWriter = true // The acceptor is the single writer.
                    }
                );

                // Bind the socket to the specified IP address and port
                _listenSocket!.Bind(new IPEndPoint(_options.IpAddress, _options.Port));
                // Start listening for incoming connection requests with a specified backlog
                _listenSocket.Listen(_options.ListenBacklog);

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
            }
            catch (Exception ex)
            {
                _logger
                    .CriticalAsync(
                        "Failed to start TCP server.",
                        ex,
                        cancellationToken: cancellationToken
                    )
                    .ConfigureAwait(false);
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
            cancellationToken: cancellationToken
        );
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                // Asynchronously accept a client socket.
                // If the channel is full, the subsequent WriteAsync will pause, providing backpressure.
                Socket acceptedSocket = await _listenSocket!.AcceptAsync(cancellationToken);

                // Wrap the accepted Socket in a TcpClient to maintain compatibility with ProcessConnectionAsync
                var client = new TcpClient { Client = acceptedSocket };

                await _connectionChannel!.Writer.WriteAsync(client, cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
            // Expected when the server is stopping due to cancellation token.
        }
        catch (SocketException sex) when (sex.SocketErrorCode == SocketError.OperationAborted)
        {
            // Expected when the listening socket is closed (e.g., by StopServer).
        }
        catch (Exception ex)
        {
            await _logger.CriticalAsync(
                "Acceptor loop encountered a fatal error.",
                ex,
                cancellationToken: cancellationToken
            );
        }
        finally
        {
            // Mark the channel as complete, signaling to workers that no more items will be added.
            _connectionChannel?.Writer.Complete();
            await _logger.InfoAsync("TCP Acceptor stopped.", cancellationToken: cancellationToken);
        }
    }

    /// <summary>
    /// The "Consumer" task. A pool of these workers will process connections from the channel.
    /// </summary>
    private async Task RunWorkerAsync(int workerId, CancellationToken cancellationToken)
    {
        await _logger.InfoAsync(
            $"Worker #{workerId} started.",
            cancellationToken: cancellationToken
        );
        try
        {
            // This will loop until the channel is marked as complete and is empty.
            await foreach (var client in _connectionChannel!.Reader.ReadAllAsync(cancellationToken))
            {
                // Process one client connection. A try/catch ensures that one failed client
                // does not terminate the entire worker.
                try
                {
                    await ProcessConnectionAsync(client, cancellationToken);
                }
                catch (Exception ex)
                {
                    var remoteEndPoint = SafeGetIpEndPoint(client);
                    await _logger.ErrorAsync(
                        $"Unhandled exception in connection processing for '{remoteEndPoint}'. Worker continues.",
                        ex,
                        cancellationToken: cancellationToken
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
                cancellationToken: cancellationToken
            );
        }
        finally
        {
            await _logger.InfoAsync(
                $"Worker #{workerId} stopped.",
                cancellationToken: cancellationToken
            );
        }
    }

    /// <summary>
    /// Handles the lifecycle of a single client connection using System.IO.Pipelines.
    /// This method remains largely the same as in V2, as it operates on TcpClient.
    /// </summary>
    private async Task ProcessConnectionAsync(TcpClient client, CancellationToken cancellationToken)
    {
        using (client) // Ensures TcpClient and its underlying Socket are disposed
        {
            var remoteEndPoint = SafeGetIpEndPoint(client);
            await _logger.DebugAsync(
                $"Processing connection from: {remoteEndPoint}",
                cancellationToken: cancellationToken
            );

            var stream = client.GetStream();
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
                // Normal cancellation
            }
            catch (Exception ex)
            {
                await _logger.ErrorAsync(
                    $"Error processing connection from {remoteEndPoint}.",
                    ex,
                    cancellationToken: cancellationToken
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
                    cancellationToken: cancellationToken
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
            StopServer(); // Call the private method to perform immediate shutdown actions
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
                cancellationToken: cancellationToken
            );
        }
    }

    /// <summary>
    /// This private method contains the non-async part of the shutdown.
    /// It can be called safely from StopAsync and DisposeAsync.
    /// </summary>
    private void StopServer()
    {
        lock (_stateLock)
        {
            if (!_isRunning)
                return;

            _isRunning = false;
            _serverCts.Cancel(); // Signal cancellation to acceptor and worker tasks
            _listenSocket?.Close(); // Close the listening socket to unblock AcceptAsync and stop accepting new connections
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

        // Ensure the server is stopped gracefully before disposing resources
        await StopAsync(CancellationToken.None);

        _serverCts.Dispose(); // Dispose the CancellationTokenSource
        _listenSocket?.Dispose(); // Explicitly dispose the listening socket
    }

    /// <summary>
    /// Safely retrieves the remote IP endpoint of a TcpClient.
    /// </summary>
    private static IPEndPoint? SafeGetIpEndPoint(TcpClient client)
    {
        try
        {
            return client.Client.RemoteEndPoint as IPEndPoint;
        }
        catch
        {
            // Can throw if the socket is already closed.
            return null;
        }
    }
}

public sealed class TcpNodeOptionsV3
{
    /// <summary>
    /// The IP address the server will listen on. Required.
    /// </summary>
    public IPAddress IpAddress { get; set; } = IPAddress.Any;

    /// <summary>
    /// The port the server will listen on.
    /// </summary>
    public int Port { get; set; }

    /// <summary>
    /// The maximum number of connections that can be processed concurrently.
    /// This directly translates to the number of "worker" tasks.
    /// </summary>
    public int MaxConcurrentConnections { get; set; } = 100;

    /// <summary>
    /// The capacity of the internal channel that buffers accepted connections before they are processed.
    /// It's recommended to set this to a value slightly higher than MaxConcurrentConnections.
    /// </summary>
    public int ChannelCapacity { get; set; } = 120;

    /// <summary>
    /// An optional handler for exceptions that occur during connection processing.
    /// The IPEndPoint might be null if the exception occurs before the endpoint is resolved.
    /// </summary>
    public Action<Exception, IPEndPoint?>? ExceptionHandler { get; set; }

    /// <summary>
    /// The timeout for waiting for all tasks to shut down gracefully when StopAsync is called.
    /// </summary>
    public TimeSpan StopTimeout { get; set; } = TimeSpan.FromSeconds(15);

    /// <summary>
    /// The maximum length of the pending connections queue.
    /// This is passed to the Socket.Listen method.
    /// </summary>
    public int ListenBacklog { get; set; } = 100; // Default backlog value
}
