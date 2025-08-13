namespace Pico.Node;

/// <summary>
/// Optimized TCP server combining advantages of V3 and V4 implementations
/// - Supports both IPv4 and IPv6 (V3 advantage)
/// - Intelligent thread scaling based on CPU cores (V4 advantage)
/// - Enhanced logging and robustness features
/// </summary>
public sealed class TcpNodeV5 : INode
{
    // Dependencies and Configuration
    private readonly TcpNodeOptionsV5 _options;
    private readonly ILogger<TcpNodeV5> _logger;
    private readonly Func<IPipelineHandler> _handlerFactory;
    private readonly Socket _serverSocket;

    // State Management
    private readonly CancellationTokenSource _serverCts = new();
    private readonly object _stateLock = new();
    private bool _isRunning;
    private bool _isDisposed;

    // Core Architecture Components
    private Channel<TcpClient>? _connectionChannel;
    private Task? _acceptorTask;
    private readonly List<Task> _workerTasks = [];

    /// <summary>
    /// Initializes a new TCP server instance
    /// </summary>
    /// <param name="options">Configuration parameters</param>
    /// <param name="handlerFactory">Factory for connection handlers</param>
    /// <param name="logger">Logger instance</param>
    public TcpNodeV5(
        TcpNodeOptionsV5 options,
        Func<IPipelineHandler> handlerFactory,
        ILogger<TcpNodeV5> logger
    )
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _handlerFactory = handlerFactory ?? throw new ArgumentNullException(nameof(handlerFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        // Create socket with dynamic address family (supports IPv4/IPv6)
        _serverSocket = new Socket(
            _options.IpAddress.AddressFamily, // Dynamic family selection
            SocketType.Stream,
            ProtocolType.Tcp
        );

        // Enable port reuse for faster restarts
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
            _logger.WarningAsync("Failed to set ReuseAddress option", ex).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Starts the TCP server and begins accepting connections
    /// </summary>
    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        lock (_stateLock)
        {
            if (_isRunning || _isDisposed)
                return Task.CompletedTask;

            try
            {
                // Create bounded channel with backpressure
                _connectionChannel = Channel.CreateBounded<TcpClient>(
                    new BoundedChannelOptions(_options.ChannelCapacity)
                    {
                        FullMode = BoundedChannelFullMode.Wait,
                        SingleReader = false,
                        SingleWriter = true
                    }
                );

                // Bind and listen
                _serverSocket.Bind(new IPEndPoint(_options.IpAddress, _options.Port));
                _serverSocket.Listen(_options.ListenBacklog);
                _isRunning = true;

                // Link cancellation tokens
                var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
                    cancellationToken,
                    _serverCts.Token
                );

                // Start tasks
                _acceptorTask = RunAcceptorAsync(linkedCts.Token);
                for (var i = 0; i < _options.MaxConcurrentConnections; i++)
                {
                    _workerTasks.Add(RunWorkerAsync(i, linkedCts.Token));
                }

                // Enhanced startup logging (V4 improvement)
                _logger
                    .InfoAsync(
                        $"TCP server started on {_options.IpAddress}:{_options.Port} "
                            + $"(Workers: {_options.MaxConcurrentConnections}, "
                            + $"Channel: {_options.ChannelCapacity}, "
                            + $"Backlog: {_options.ListenBacklog})",
                        cancellationToken: linkedCts.Token
                    )
                    .ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.CriticalAsync("Server startup failed", ex).ConfigureAwait(false);
                StopServer();
                throw;
            }
        }
        return Task.CompletedTask;
    }

    /// <summary>
    /// Connection acceptor loop (producer)
    /// </summary>
    private async Task RunAcceptorAsync(CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                // Accept socket with cancellation support
                Socket acceptedSocket = await _serverSocket.AcceptAsync(cancellationToken);

                // Wrap in TcpClient for handler compatibility
                var client = new TcpClient { Client = acceptedSocket };

                // Write to channel with backpressure
                await _connectionChannel!.Writer.WriteAsync(client, cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
            // Normal shutdown
        }
        // V3 precision exception handling
        catch (SocketException sex) when (sex.SocketErrorCode == SocketError.OperationAborted)
        {
            // Expected during shutdown
        }
        catch (Exception ex)
        {
            await _logger.CriticalAsync("Acceptor fatal error", ex);
        }
        finally
        {
            _connectionChannel?.Writer.Complete();
            await _logger.InfoAsync("Acceptor stopped");
        }
    }

    /// <summary>
    /// Connection processor loop (consumer)
    /// </summary>
    private async Task RunWorkerAsync(int workerId, CancellationToken cancellationToken)
    {
        try
        {
            await foreach (var client in _connectionChannel!.Reader.ReadAllAsync(cancellationToken))
            {
                try
                {
                    await ProcessConnectionAsync(client, cancellationToken);
                }
                catch (Exception ex)
                {
                    var endpoint = SafeGetIpEndPoint(client);
                    await _logger.ErrorAsync($"Processing failed for {endpoint}", ex);
                    _options.ExceptionHandler?.Invoke(ex, endpoint);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Shutdown requested
        }
        catch (Exception ex)
        {
            await _logger.CriticalAsync($"Worker #{workerId} crashed", ex);
        }
        finally
        {
            await _logger.InfoAsync($"Worker #{workerId} stopped");
        }
    }

    /// <summary>
    /// Processes individual client connection
    /// </summary>
    private async Task ProcessConnectionAsync(TcpClient client, CancellationToken cancellationToken)
    {
        using (client)
        {
            var remoteEp = SafeGetIpEndPoint(client);
            await _logger.DebugAsync($"New connection: {remoteEp}");

            // Get network stream (automatically disposed with client)
            using var stream = client.GetStream();
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
                // Normal cancellation flow
            }
            catch (Exception ex)
            {
                await _logger.ErrorAsync($"Connection error: {remoteEp}", ex);
                _options.ExceptionHandler?.Invoke(ex, remoteEp);
            }
            finally
            {
                // Ensure proper cleanup of pipeline resources
                await reader.CompleteAsync();
                await writer.CompleteAsync();

                // Safe resource disposal based on interface implementation
                switch (handler)
                {
                    case IAsyncDisposable asyncDisposable:
                        await asyncDisposable.DisposeAsync();
                        break;
                    case IDisposable disposable:
                        disposable.Dispose();
                        break;
                }

                await _logger.DebugAsync($"Closed connection: {remoteEp}");
            }
        }
    }

    /// <summary>
    /// Stops server with graceful shutdown
    /// </summary>
    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        lock (_stateLock)
        {
            if (!_isRunning)
                return;
            StopServer(); // Initiate shutdown
        }

        // Collect all running tasks
        var tasks = new List<Task>();
        if (_acceptorTask != null)
            tasks.Add(_acceptorTask);
        tasks.AddRange(_workerTasks);

        try
        {
            // Wait for graceful shutdown with timeout
            using var timeoutCts = new CancellationTokenSource(_options.StopTimeout);
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken,
                timeoutCts.Token
            );

            await Task.WhenAll(tasks).WaitAsync(linkedCts.Token);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            await _logger.WarningAsync("Shutdown cancelled by caller");
        }
        catch (Exception ex)
        {
            await _logger.WarningAsync(
                $"Graceful shutdown failed after {_options.StopTimeout.TotalSeconds}s",
                ex
            );
        }
    }

    /// <summary>
    /// Internal server shutdown procedure
    /// </summary>
    private void StopServer()
    {
        lock (_stateLock)
        {
            if (!_isRunning)
                return;

            _isRunning = false;
            _serverCts.Cancel();

            // V4 socket closure with exception handling
            try
            {
                _serverSocket.Close();
            }
            catch (Exception ex)
            {
                _logger.WarningAsync("Error closing server socket", ex).ConfigureAwait(false);
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_isDisposed)
            return;
        _isDisposed = true;

        await StopAsync();
        _serverCts.Dispose();
        _serverSocket.Dispose();
    }

    /// <summary>
    /// Safely retrieves client endpoint information
    /// </summary>
    private static IPEndPoint? SafeGetIpEndPoint(TcpClient client)
    {
        try
        {
            return client.Client.RemoteEndPoint as IPEndPoint;
        }
        catch
        {
            return null; // Socket may be closed
        }
    }
}

/// <summary>
/// Unified configuration options for TCP server
/// </summary>
public sealed class TcpNodeOptionsV5
{
    /// <summary>
    /// IP address to listen on (supports IPv4/IPv6)
    /// </summary>
    public IPAddress IpAddress { get; set; } = IPAddress.Any;

    /// <summary>
    /// TCP port number
    /// </summary>
    public int Port { get; set; } = 8080;

    /// <summary>
    /// Maximum concurrent connections (default: CPU core count)
    /// </summary>
    public int MaxConcurrentConnections { get; set; } = Environment.ProcessorCount;

    /// <summary>
    /// Connection queue buffer size (V4 advantage)
    /// </summary>
    public int ChannelCapacity { get; set; } = 1000;

    /// <summary>
    /// OS-level connection backlog size
    /// </summary>
    public int ListenBacklog { get; set; } = 100;

    /// <summary>
    /// Graceful shutdown timeout
    /// </summary>
    public TimeSpan StopTimeout { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Custom exception handler
    /// </summary>
    public Action<Exception, IPEndPoint?>? ExceptionHandler { get; set; }
}
