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

    // The exception handler now receives a rich IPEndPoint object
    private readonly Action<Exception, IPEndPoint>? _exceptionHandler;

    // Runtime state
    private readonly ConcurrentDictionary<Task, bool> _activeTasks = new();
    private volatile bool _isDisposed;
    private readonly CancellationTokenSource _serverCts = new();
    private readonly Lock _stateLock = new();
    private bool _isRunning;
    private Task? _serverTask; // Task to run the main server loop

    public TcpNode(
        IPAddress ipAddress,
        ushort port,
        Func<ITcpHandler> tcpHandlerFactory,
        ILogger<TcpNode> logger,
        int maxConcurrentConnections = 100,
        // The exception handler signature is improved to use IPEndPoint
        Action<Exception, IPEndPoint>? exceptionHandler = null
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

    /// <summary>
    /// Starts the TCP server as a background operation.
    /// This method returns immediately after initiating the server startup.
    /// </summary>
    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        lock (_stateLock)
        {
            if (_isRunning)
                return Task.CompletedTask;

            try
            {
                _listener.Start();
                _isRunning = true;

                // Start the server loop as a background task and store it
                _serverTask = RunServerAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.CriticalAsync("Failed to initialize TCP listener.", ex, cancellationToken);
                _isRunning = false; // Ensure state is reverted on failure
                throw;
            }
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// The main server loop for accepting new client connections.
    /// </summary>
    private async Task RunServerAsync(CancellationToken cancellationToken)
    {
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken,
            _serverCts.Token
        );
        var serverToken = linkedCts.Token;

        await _logger.InfoAsync($"TCP server started on {_ipAddress}:{_port}", serverToken);

        try
        {
            while (!serverToken.IsCancellationRequested)
            {
                // Wait for an available connection slot before accepting a new client
                await _connectionSemaphore.WaitAsync(serverToken).ConfigureAwait(false);

                var client = await _listener
                    .AcceptTcpClientAsync(serverToken)
                    .ConfigureAwait(false);

                // Create a task to process the client connection
                var clientTask = ProcessClientAsync(client, serverToken);

                // Track the task for graceful shutdown
                _activeTasks.TryAdd(clientTask, true);

                // Attach a continuation to clean up the task from the tracking dictionary upon completion.
                // This runs in the background and does not block the accept loop.
                _ = clientTask.ContinueWith(
                    t =>
                    {
                        _activeTasks.TryRemove(t, out _);
                    },
                    TaskContinuationOptions.ExecuteSynchronously
                );
            }
        }
        catch (OperationCanceledException)
        {
            // This is expected during a normal shutdown.
        }
        catch (Exception ex)
        {
            // A fatal error occurred in the server loop itself
            await _logger.CriticalAsync(
                "TCP server accept loop fatal error. The server is stopping.",
                ex,
                CancellationToken.None
            );
            StopServer(); // Trigger a full stop
        }
        finally
        {
            lock (_stateLock)
            {
                _isRunning = false;
            }
            await _logger.InfoAsync("TCP server loop has stopped.", CancellationToken.None);
        }
    }

    /// <summary>
    /// Stops the TCP server gracefully.
    /// </summary>
    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        StopServer();

        if (!_activeTasks.IsEmpty)
        {
            var shutdownTimeout = TimeSpan.FromSeconds(5);
            using var timeoutCts = new CancellationTokenSource(shutdownTimeout);
            using var combinedCts = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken,
                timeoutCts.Token
            );

            try
            {
                await Task.WhenAll(_activeTasks.Keys)
                    .WaitAsync(combinedCts.Token)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested)
            {
                await _logger.WarningAsync(
                    $"Timeout waiting for {_activeTasks.Count} active client tasks to complete during shutdown.",
                    cancellationToken: CancellationToken.None
                );
            }
            catch (OperationCanceledException)
            {
                // Shutdown was cancelled by the external token, which is fine.
            }
        }
    }

    /// <summary>
    /// Atomically initiates the server shutdown process.
    /// </summary>
    private void StopServer()
    {
        lock (_stateLock)
        {
            // StopServer can be called multiple times from StopAsync and DisposeAsync,
            // so we check if it's already in the process of stopping.
            if (!_serverCts.IsCancellationRequested)
            {
                // Signal cancellation to all operations (accept loop, client handlers).
                _serverCts.Cancel();
            }

            // Stopping the listener will cause AcceptTcpClientAsync to throw an exception,
            // effectively unblocking the server loop.
            try
            {
                if (_listener.Server.IsBound)
                {
                    _listener.Stop();
                }
            }
            catch (Exception ex)
            {
                _logger.WarningAsync(
                    "Exception while stopping TCP listener, this may happen during shutdown.",
                    ex
                );
            }
        }
    }

    /// <summary>
    /// Handles a single client connection from start to finish.
    /// </summary>
    private async Task ProcessClientAsync(TcpClient client, CancellationToken cancellationToken)
    {
        // Use IPEndPoint for richer data
        var remoteEndPoint = client.Client.RemoteEndPoint as IPEndPoint;
        var endpointString = remoteEndPoint?.ToString() ?? "unknown";

        try
        {
            await _logger.DebugAsync(
                $"Accepted new client connection from {endpointString}",
                cancellationToken
            );

            // Using 'using' statements ensures the client and stream are always disposed
            using (client)
            await using (var stream = client.GetStream())
            {
                var handler = _tcpHandlerFactory();
                await handler.HandleAsync(stream, cancellationToken).ConfigureAwait(false);
            }

            await _logger.DebugAsync(
                $"Client {endpointString} disconnected gracefully.",
                cancellationToken
            );
        }
        catch (OperationCanceledException)
        {
            // This is an expected exception when the server is shutting down.
            await _logger.InfoAsync(
                $"Client {endpointString} connection canceled.",
                cancellationToken
            );
        }
        catch (Exception ex)
        {
            await _logger.ErrorAsync(
                $"An error occurred while processing client {endpointString}.",
                ex,
                cancellationToken
            );

            // Invoke the external exception handler if it's provided
            if (remoteEndPoint != null)
            {
                _exceptionHandler?.Invoke(ex, remoteEndPoint);
            }
        }
        finally
        {
            // CRITICAL: Always release the semaphore slot to allow another client to connect.
            _connectionSemaphore.Release();
        }
    }

    /// <summary>
    /// Disposes all managed and unmanaged resources.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        if (_isDisposed)
            return;

        _isDisposed = true;

        // Initiate shutdown
        StopServer();

        // Wait for the main server task to complete with a timeout
        if (_serverTask != null)
        {
            try
            {
                await _serverTask.WaitAsync(TimeSpan.FromSeconds(2)).ConfigureAwait(false);
            }
            catch (TimeoutException)
            {
                await _logger.WarningAsync(
                    "Timeout waiting for server loop to exit during disposal."
                );
            }
        }

        // Wait for active client tasks to complete with a timeout
        if (!_activeTasks.IsEmpty)
        {
            try
            {
                await Task.WhenAll(_activeTasks.Keys)
                    .WaitAsync(TimeSpan.FromSeconds(5))
                    .ConfigureAwait(false);
            }
            catch (TimeoutException)
            {
                await _logger.WarningAsync(
                    "Timeout waiting for active tasks to complete during disposal."
                );
            }
        }

        // Final resource cleanup
        _connectionSemaphore.Dispose();
        _serverCts.Dispose();
    }
}
