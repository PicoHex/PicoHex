namespace Pico.Node;

public sealed class TcpNode : INode
{
    // Server configuration parameters
    private readonly IPAddress _ipAddress;
    private readonly ushort _port;
    private readonly ILogger<TcpNode> _logger;
    private readonly SemaphoreSlim _connectionSemaphore;
    private readonly TcpListener _listener;
    private readonly Func<ITcpHandler> _tcpHandlerFactory;

    // Runtime state management
    private readonly ConcurrentDictionary<Task, bool> _activeTasks = new();
    private volatile bool _isDisposed;
    private readonly CancellationTokenSource _serverCts = new();

    // Cleanup and error handling configuration
    private readonly TimeSpan _disposeTimeout;
    private readonly Action<Exception, string>? _exceptionHandler;

    public TcpNode(
        IPAddress ipAddress,
        ushort port,
        Func<ITcpHandler> tcpHandlerFactory,
        ILogger<TcpNode> logger,
        int maxConcurrentConnections = 100,
        TimeSpan? disposeTimeout = null,
        Action<Exception, string>? exceptionHandler = null
    )
    {
        // Validate required dependencies
        ArgumentNullException.ThrowIfNull(ipAddress);
        ArgumentNullException.ThrowIfNull(tcpHandlerFactory);
        ArgumentNullException.ThrowIfNull(logger);

        _ipAddress = ipAddress;
        _port = port;
        _tcpHandlerFactory = tcpHandlerFactory;
        _logger = logger;
        _connectionSemaphore = new SemaphoreSlim(maxConcurrentConnections);
        _listener = new TcpListener(_ipAddress, _port);
        _disposeTimeout = disposeTimeout ?? TimeSpan.FromSeconds(5);
        _exceptionHandler = exceptionHandler;
    }

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        // Combine server lifetime token with external cancellation token
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken,
            _serverCts.Token
        );

        var serverToken = linkedCts.Token;
        _listener.Start();
        await _logger.InfoAsync($"TCP server started on {_ipAddress}:{_port}", serverToken);

        try
        {
            // Main server loop - runs until cancellation is requested
            while (!serverToken.IsCancellationRequested)
            {
                // Throttle connections using semaphore
                await _connectionSemaphore.WaitAsync(serverToken).ConfigureAwait(false);
                TcpClient? client = null;

                try
                {
                    // Accept incoming client connection
                    client = await _listener
                        .AcceptTcpClientAsync(serverToken)
                        .ConfigureAwait(false);

                    await _logger.InfoAsync(
                        $"Accepted new client connection {client.Client.RemoteEndPoint}",
                        serverToken
                    );

                    // Create and track client processing task
                    var clientTask = ProcessClientAsync(client, serverToken)
                        .ContinueWith(
                            t =>
                            {
                                // Cleanup task tracking
                                _activeTasks.TryRemove(t, out _);

                                // Handle unobserved task exceptions
                                if (t is { IsFaulted: true, Exception: not null })
                                {
                                    _logger
                                        .ErrorAsync(
                                            "Unhandled exception in client task",
                                            t.Exception,
                                            cancellationToken: serverToken
                                        )
                                        .GetAwaiter()
                                        .GetResult();
                                }
                            },
                            TaskContinuationOptions.ExecuteSynchronously
                        );

                    // Register client task for lifecycle management
                    _activeTasks.TryAdd(clientTask, true);
                }
                catch (Exception ex)
                {
                    // Handle connection acceptance errors asynchronously
                    _ = HandleAcceptExceptionAsync(ex, client, serverToken);
                }
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Log fatal server errors
            await _logger.ErrorAsync("TCP server fatal error", ex, cancellationToken: serverToken);
            throw;
        }
        finally
        {
            // Graceful shutdown sequence
            Stop();
            await _logger.InfoAsync("TCP server stopped", serverToken);
        }
    }

    private async Task HandleAcceptExceptionAsync(
        Exception ex,
        TcpClient? client,
        CancellationToken token
    )
    {
        // Cleanup resources
        client?.Dispose();
        _connectionSemaphore.Release();

        switch (ex)
        {
            case OperationCanceledException:
                return; // Normal cancellation during shutdown

            case SocketException socketEx:
                // Determine if socket error is critical
                if (IsCriticalSocketError(socketEx.SocketErrorCode))
                {
                    await _logger.CriticalAsync(
                        $"Critical socket error: {socketEx.SocketErrorCode}",
                        socketEx,
                        token
                    );
                    // Initiate server shutdown for unrecoverable errors
                    await _serverCts.CancelAsync();
                }
                else
                {
                    await _logger.WarningAsync("Non-critical socket exception", socketEx, token);
                }
                break;

            default:
                await _logger.ErrorAsync(
                    "Error accepting client connection",
                    ex,
                    cancellationToken: token
                );
                break;
        }
    }

    private async Task ProcessClientAsync(TcpClient client, CancellationToken cancellationToken)
    {
        string? endpoint = null;
        try
        {
            endpoint = client.Client.RemoteEndPoint?.ToString() ?? "unknown";

            using (client)
            await using (var stream = client.GetStream())
            {
                // Create protocol handler for this connection
                var handler = _tcpHandlerFactory();
                await handler.HandleAsync(stream, cancellationToken).ConfigureAwait(false);
            }

            await _logger.InfoAsync($"Client {endpoint} disconnected", cancellationToken);
        }
        catch (OperationCanceledException)
        {
            await _logger.InfoAsync(
                $"Client {endpoint} disconnected (canceled)",
                cancellationToken
            );
        }
        catch (Exception ex)
        {
            await _logger.ErrorAsync(
                $"Client {endpoint} processing error",
                ex,
                cancellationToken: cancellationToken
            );

            // Invoke custom exception handler if configured
            if (endpoint != null)
                _exceptionHandler?.Invoke(ex, endpoint);
        }
        finally
        {
            // Ensure semaphore slot is released
            _connectionSemaphore.Release();
        }
    }

    /// <summary>
    /// Determines if a socket error is critical and requires server shutdown
    /// </summary>
    /// <param name="error">Socket error code</param>
    /// <returns>True if error is critical, false otherwise</returns>
    private static bool IsCriticalSocketError(SocketError error) =>
        error switch
        {
            SocketError.AccessDenied => true, // Permission issues
            SocketError.AddressAlreadyInUse => true, // Port conflict
            SocketError.AddressNotAvailable => true, // Invalid binding
            SocketError.InvalidArgument => true, // Configuration errors
            _ => false // Non-critical errors (timeouts, resets, etc)
        };

    /// <summary>
    /// Stops accepting new connections and signals cancellation
    /// </summary>
    private void Stop()
    {
        if (_listener.Server.IsBound)
        {
            _listener.Stop();
        }
        _serverCts.Cancel();
    }

    public async Task StopAsync(TimeSpan? timeout = null)
    {
        Stop();
        var waitTimeout = timeout ?? TimeSpan.FromSeconds(10);

        // Wait for active client tasks to complete
        if (!_activeTasks.IsEmpty)
        {
            await Task.WhenAll(_activeTasks.Keys).WaitAsync(waitTimeout).ConfigureAwait(false);
        }
    }

    public void Dispose()
    {
        if (_isDisposed)
            return;

        try
        {
            Stop();
            // Wait for active tasks with timeout
            Task.WaitAll(_activeTasks.Keys.ToArray(), _disposeTimeout);
        }
        finally
        {
            // Cleanup managed resources
            _connectionSemaphore.Dispose();
            _serverCts.Dispose();
            _listener.Server.Dispose();
            _isDisposed = true;
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_isDisposed)
            return;

        try
        {
            Stop();

            // Async wait for client tasks with timeout
            if (!_activeTasks.IsEmpty)
            {
                await Task.WhenAll(_activeTasks.Keys)
                    .WaitAsync(_disposeTimeout)
                    .ConfigureAwait(false);
            }
        }
        finally
        {
            // Cleanup resources
            _connectionSemaphore.Dispose();
            _serverCts.Dispose();
            _listener.Server.Dispose();
            _isDisposed = true;
        }
    }
}
