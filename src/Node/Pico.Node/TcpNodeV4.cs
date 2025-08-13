namespace Pico.Node;

/// <summary>
/// A high-performance TCP server implementation using raw Sockets and System.IO.Pipelines,
/// based on a producer-consumer pattern with backpressure.
/// </summary>
public sealed class TcpNodeV4 : INode
{
    // Dependencies and Configuration
    private readonly TcpNodeOptionsV4 _options;
    private readonly ILogger<TcpNodeV4> _logger;
    private readonly Func<IPipelineHandler> _handlerFactory;
    private readonly Socket _serverSocket; // Replaced TcpListener with raw Socket

    // State Management
    private readonly CancellationTokenSource _serverCts = new();
    private readonly Lock _stateLock = new();
    private bool _isRunning;
    private bool _isDisposed;

    // Core Architecture Components
    private Channel<TcpClient>? _connectionChannel;
    private Task? _acceptorTask;
    private readonly List<Task> _workerTasks = [];

    /// <summary>
    /// Initializes a new instance of the TcpNodeV4 class.
    /// </summary>
    /// <param name="options">Configuration options for the TCP node.</param>
    /// <param name="handlerFactory">A factory function to create IPipelineHandler instances for each connection.</param>
    /// <param name="logger">Logger instance for logging server events.</param>
    public TcpNodeV4(
        TcpNodeOptionsV4 options,
        Func<IPipelineHandler> handlerFactory,
        ILogger<TcpNodeV4> logger
    )
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _handlerFactory = handlerFactory ?? throw new ArgumentNullException(nameof(handlerFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        // Initialize the raw server socket
        _serverSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

        // Enable port reuse for faster restarts, a professional touch.
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
            // Log a warning if setting the option fails, but don't block startup.
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
                _serverSocket.Bind(new IPEndPoint(_options.IpAddress, _options.Port));
                // Start listening for incoming connections with a specified backlog
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

                _logger
                    .InfoAsync(
                        $"TCP server started on {_options.IpAddress}:{_options.Port} (Max connections: {_options.MaxConcurrentConnections}, Channel capacity: {_options.ChannelCapacity}, Listen backlog: {_options.ListenBacklog}).",
                        cancellationToken: linkedCts.Token
                    )
                    .ConfigureAwait(false);
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
                Socket acceptedSocket = await _serverSocket.AcceptAsync(cancellationToken);

                // Wrap the accepted Socket into a TcpClient to maintain compatibility with existing handlers.
                var client = new TcpClient { Client = acceptedSocket };

                await _connectionChannel!.Writer.WriteAsync(client, cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
            // Expected when the server is stopping.
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
    /// </summary>
    private async Task ProcessConnectionAsync(TcpClient client, CancellationToken cancellationToken)
    {
        using (client) // Ensure TcpClient (and its underlying Socket) is disposed
        {
            var remoteEndPoint = SafeGetIpEndPoint(client);
            await _logger.DebugAsync(
                $"Processing connection from: {remoteEndPoint}",
                cancellationToken: cancellationToken
            );

            // Get the NetworkStream from the TcpClient
            var stream = client.GetStream();
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
                _logger.WarningAsync("Error closing server socket.", ex).ConfigureAwait(false);
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
    /// Safely retrieves the IPEndPoint of a TcpClient.
    /// </summary>
    /// <param name="client">The TcpClient instance.</param>
    /// <returns>The IPEndPoint or null if it cannot be retrieved.</returns>
    private static IPEndPoint? SafeGetIpEndPoint(TcpClient client)
    {
        try
        {
            return client.Client.RemoteEndPoint as IPEndPoint;
        }
        catch
        {
            // Can throw if the socket is already closed or disposed.
            return null;
        }
    }
}

public sealed class TcpNodeOptionsV4
{
    /// <summary>
    /// The IP address the server will listen on.
    /// </summary>
    public IPAddress IpAddress { get; set; } = IPAddress.Any;

    /// <summary>
    /// The port the server will listen on.
    /// </summary>
    public int Port { get; set; } = 8080;

    /// <summary>
    /// The maximum number of concurrent connections the server can handle.
    /// This also determines the number of worker tasks.
    /// </summary>
    public int MaxConcurrentConnections { get; set; } = Environment.ProcessorCount;

    /// <summary>
    /// The capacity of the internal channel for accepted connections.
    /// This acts as a buffer for backpressure.
    /// </summary>
    public int ChannelCapacity { get; set; } = 1000;

    /// <summary>
    /// The maximum length of the pending connections queue.
    /// This is passed to Socket.Listen().
    /// </summary>
    public int ListenBacklog { get; set; } = 100; // Default backlog

    /// <summary>
    /// Timeout for graceful shutdown of all tasks.
    /// </summary>
    public TimeSpan StopTimeout { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Optional exception handler for unhandled exceptions during connection processing.
    /// </summary>
    public Action<Exception, IPEndPoint?>? ExceptionHandler { get; set; }
}
