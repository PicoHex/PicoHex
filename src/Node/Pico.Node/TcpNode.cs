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
    private readonly Action<Exception, IPEndPoint>? _exceptionHandler;

    // Runtime state
    private readonly ConcurrentBag<Task> _activeTasks = new();
    private volatile bool _isDisposed;
    private readonly CancellationTokenSource _serverCts = new();
    private readonly Lock _stateLock = new();
    private bool _isRunning;
    private Task? _serverTask;
    private readonly TaskCompletionSource _serverReadyTcs =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    public TcpNode(
        IPAddress ipAddress,
        ushort port,
        Func<ITcpHandler> tcpHandlerFactory,
        ILogger<TcpNode> logger,
        int maxConcurrentConnections = 100,
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
    /// Starts the TCP server. Returns a task that completes when the server is ready.
    /// </summary>
    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        lock (_stateLock)
        {
            if (_isRunning)
                return _serverReadyTcs.Task;

            try
            {
                _listener.Start();
                _isRunning = true;
                _serverTask = RunServerAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                // Ensure log is written before throwing
                _ = _logger.CriticalAsync(
                    "Failed to start TCP listener",
                    ex,
                    cancellationToken: cancellationToken
                );
                _isRunning = false;
                _serverReadyTcs.TrySetException(ex); // Propagate error
                throw;
            }
        }
        return _serverReadyTcs.Task;
    }

    private async Task RunServerAsync(CancellationToken cancellationToken)
    {
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken,
            _serverCts.Token
        );
        var serverToken = linkedCts.Token;

        try
        {
            await _logger.InfoAsync($"TCP server started on {_ipAddress}:{_port}", serverToken);
            _serverReadyTcs.TrySetResult(); // Signal server is ready

            while (!serverToken.IsCancellationRequested)
            {
                await _connectionSemaphore.WaitAsync(serverToken).ConfigureAwait(false);
                var client = await _listener
                    .AcceptTcpClientAsync(serverToken)
                    .ConfigureAwait(false);

                var clientTask = ProcessClientAsync(client, serverToken);
                _activeTasks.Add(clientTask);
            }
        }
        catch (OperationCanceledException)
        { /* Normal shutdown */
        }
        catch (Exception ex)
        {
            await _logger.CriticalAsync("Server loop fatal error", ex, CancellationToken.None);
            StopServer();
        }
        finally
        {
            lock (_stateLock)
                _isRunning = false;
            await _logger.InfoAsync("Server stopped", CancellationToken.None);
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        StopServer();

        if (!_activeTasks.IsEmpty)
        {
            using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken,
                timeoutCts.Token
            );

            try
            {
                await Task.WhenAll(_activeTasks).WaitAsync(linkedCts.Token);
            }
            catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested)
            {
                await _logger.WarningAsync(
                    $"Timeout waiting for {_activeTasks.Count} tasks",
                    cancellationToken: linkedCts.Token
                );
            }
        }
    }

    private void StopServer()
    {
        lock (_stateLock)
        {
            if (_serverCts.IsCancellationRequested)
                return;
            _serverCts.Cancel();

            try
            {
                _listener.Stop();
            } // Safe even if not bound
            catch (Exception ex)
            {
                _ = _logger.WarningAsync("Error stopping listener", ex);
            }
        }
    }

    private async Task ProcessClientAsync(TcpClient client, CancellationToken cancellationToken)
    {
        var remoteEndPoint = (client.Client.RemoteEndPoint as IPEndPoint)?.ToString() ?? "unknown";

        try
        {
            await _logger.DebugAsync(
                $"Accepted connection from {remoteEndPoint}",
                cancellationToken
            );
            using (client)
            await using (var stream = client.GetStream())
            {
                await _tcpHandlerFactory().HandleAsync(stream, cancellationToken);
            }
            await _logger.DebugAsync($"Client {remoteEndPoint} disconnected", cancellationToken);
        }
        catch (OperationCanceledException)
        {
            await _logger.InfoAsync($"Client {remoteEndPoint} canceled", cancellationToken);
        }
        catch (Exception ex)
        {
            await _logger.ErrorAsync($"Client {remoteEndPoint} error", ex, cancellationToken);
            if (client.Client.RemoteEndPoint is IPEndPoint ipEp)
            {
                try
                {
                    _exceptionHandler?.Invoke(ex, ipEp);
                }
                catch (Exception handlerEx)
                {
                    await _logger.ErrorAsync(
                        "Exception handler failed",
                        handlerEx,
                        cancellationToken
                    );
                }
            }
        }
        finally
        {
            _connectionSemaphore.Release();
            _activeTasks.TryTake(out _); // Remove self from tracking
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_isDisposed)
            return;
        _isDisposed = true;

        StopServer();

        // Wait for main server loop
        if (_serverTask != null)
        {
            try
            {
                await _serverTask.WaitAsync(TimeSpan.FromSeconds(2));
            }
            catch (TimeoutException)
            {
                await _logger.WarningAsync("Timeout waiting for server loop");
            }
        }

        // Wait for client tasks
        if (!_activeTasks.IsEmpty)
        {
            try
            {
                await Task.WhenAll(_activeTasks).WaitAsync(TimeSpan.FromSeconds(5));
            }
            catch (TimeoutException)
            {
                await _logger.WarningAsync("Timeout waiting for client tasks");
            }
        }

        _serverCts.Dispose();
        _connectionSemaphore.Dispose(); // Safe after all tasks complete
    }
}
