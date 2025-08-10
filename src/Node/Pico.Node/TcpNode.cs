namespace Pico.Node;

public sealed class TcpNode : INode
{
    // Server configuration
    private readonly IPAddress _ipAddress;
    private readonly ushort _port;
    private readonly ILogger<TcpNode> _logger;
    private readonly SemaphoreSlim _connectionSemaphore;
    private readonly TcpListener _listener;
    private readonly Func<ITcpHandler> _tcpHandlerFactory;
    private readonly Action<Exception, string>? _exceptionHandler;

    // Runtime state
    private readonly ConcurrentDictionary<Task, bool> _activeTasks = new();
    private volatile bool _isDisposed;
    private readonly CancellationTokenSource _serverCts = new();
    private readonly Lock _stateLock = new();
    private bool _isRunning;

    public TcpNode(
        IPAddress ipAddress,
        ushort port,
        Func<ITcpHandler> tcpHandlerFactory,
        ILogger<TcpNode> logger,
        int maxConcurrentConnections = 100,
        Action<Exception, string>? exceptionHandler = null
    )
    {
        ArgumentNullException.ThrowIfNull(ipAddress);
        ArgumentNullException.ThrowIfNull(tcpHandlerFactory);
        ArgumentNullException.ThrowIfNull(logger);

        _ipAddress = ipAddress;
        _port = port;
        _tcpHandlerFactory = tcpHandlerFactory;
        _logger = logger;
        _connectionSemaphore = new SemaphoreSlim(maxConcurrentConnections);
        _listener = new TcpListener(_ipAddress, _port);
        _exceptionHandler = exceptionHandler;
    }

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        lock (_stateLock)
        {
            if (_isRunning)
                return;
            _isRunning = true;
        }

        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken,
            _serverCts.Token
        );

        var serverToken = linkedCts.Token;
        _listener.Start();
        await _logger.InfoAsync($"TCP server started on {_ipAddress}:{_port}", serverToken);

        try
        {
            while (!serverToken.IsCancellationRequested)
            {
                await _connectionSemaphore.WaitAsync(serverToken).ConfigureAwait(false);
                TcpClient? client = null;

                try
                {
                    // Accept new client connection (blocking call)
                    client = await _listener
                        .AcceptTcpClientAsync(serverToken)
                        .ConfigureAwait(false);

                    // Safe endpoint retrieval with null propagation
                    var endpoint = client.Client.RemoteEndPoint?.ToString() ?? "unknown";

                    await _logger.InfoAsync(
                        $"Accepted new client connection {endpoint}",
                        serverToken
                    );

                    // Create client processing task with atomic dictionary registration
                    var clientTask = ProcessClientAsync(client, serverToken);

                    // Add to tracking BEFORE starting continuation to prevent race conditions
                    if (!_activeTasks.TryAdd(clientTask, true))
                    {
                        // Fallback: Dispose client if tracking fails
                        client.Dispose();
                        _connectionSemaphore.Release();
                        await _logger.ErrorAsync(
                            "Failed to register client task",
                            cancellationToken: serverToken
                        );
                        continue;
                    }

                    // Register cleanup handler with synchronous execution
                    clientTask.ContinueWith(
                        t =>
                        {
                            // Atomic removal from tracking
                            _activeTasks.TryRemove(t, out _);

                            // Log unhandled exceptions from task
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
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    // Handle accept failures safely
                    await HandleAcceptExceptionAsync(ex, client, serverToken);
                }
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            await _logger.ErrorAsync("TCP server fatal error", ex, cancellationToken: serverToken);
            throw;
        }
        finally
        {
            StopServer();
            lock (_stateLock)
                _isRunning = false;
            await _logger.InfoAsync("TCP server stopped", serverToken);
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        StopServer();

        if (!_activeTasks.IsEmpty)
        {
            // Wait with cancellation support
            await Task.WhenAll(_activeTasks.Keys)
                .WaitAsync(cancellationToken)
                .ConfigureAwait(false);
        }
    }

    private void StopServer()
    {
        lock (_stateLock)
        {
            if (!_isRunning)
                return;

            if (_listener.Server.IsBound)
            {
                // Graceful listener shutdown
                _listener.Stop();
            }

            if (!_serverCts.IsCancellationRequested)
            {
                // Signal cancellation to all operations
                _serverCts.Cancel();
            }
        }
    }

    private async Task HandleAcceptExceptionAsync(
        Exception ex,
        TcpClient? client,
        CancellationToken token
    )
    {
        // Ensure client disposal in all exception cases
        client?.Dispose();

        // Always release semaphore on accept failure
        _connectionSemaphore.Release();

        switch (ex)
        {
            case OperationCanceledException:
                // Normal shutdown path, no logging needed
                return;
            case SocketException socketEx:
                if (IsCriticalSocketError(socketEx.SocketErrorCode))
                {
                    await _logger.CriticalAsync(
                        $"Critical socket error: {socketEx.SocketErrorCode}",
                        socketEx,
                        token
                    );
                    StopServer(); // Stop server on critical errors
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

            // Guaranteed disposal of client and stream
            using (client)
            await using (var stream = client.GetStream())
            {
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

            // Optional external exception handling
            if (endpoint != null)
                _exceptionHandler?.Invoke(ex, endpoint);
        }
        finally
        {
            // Single-point semaphore release
            _connectionSemaphore.Release();
        }
    }

    private static bool IsCriticalSocketError(SocketError error) =>
        error switch
        {
            SocketError.AccessDenied => true,
            SocketError.AddressAlreadyInUse => true,
            SocketError.AddressNotAvailable => true,
            SocketError.InvalidArgument => true,
            _ => false
        };

    public async ValueTask DisposeAsync()
    {
        if (_isDisposed)
            return;

        try
        {
            // Stop server if still running
            StopServer();

            // Gracefully wait for active tasks to complete
            if (!_activeTasks.IsEmpty)
            {
                await Task.WhenAll(_activeTasks.Keys).ConfigureAwait(false);
            }
        }
        finally
        {
            if (!_isDisposed)
            {
                // Resource cleanup sequence
                _connectionSemaphore.Dispose();
                _serverCts.Dispose();
                _listener.Server.Dispose();
                _isDisposed = true;
            }
        }
    }
}
