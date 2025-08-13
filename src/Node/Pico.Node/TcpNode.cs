namespace Pico.Node;

public sealed class TcpNode : INode
{
    // Server configuration
    private readonly TcpNodeOptions _options;
    private readonly ILogger<TcpNode> _logger;
    private readonly SemaphoreSlim _connectionSemaphore;
    private readonly TcpListener _listener;
    private readonly Func<ITcpHandler> _tcpHandlerFactory;
    private readonly Action<Exception, IPEndPoint>? _exceptionHandler;

    // Runtime state
    private readonly ConcurrentDictionary<Task, bool> _activeTasks = new();
    private volatile bool _isDisposed;
    private readonly CancellationTokenSource _serverCts = new();
    private readonly Lock _stateLock = new();
    private bool _isRunning;
    private Task? _serverTask;
    private readonly TaskCompletionSource _serverReadyTcs =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    // Metrics
    private long _totalConnections;

    public TcpNode(
        TcpNodeOptions options,
        Func<ITcpHandler> tcpHandlerFactory,
        ILogger<TcpNode> logger
    )
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(tcpHandlerFactory);
        ArgumentNullException.ThrowIfNull(logger);

        _options = options;
        _tcpHandlerFactory = tcpHandlerFactory;
        _logger = logger;
        _connectionSemaphore = new SemaphoreSlim(options.MaxConcurrentConnections);
        _listener = new TcpListener(_options.IpAddress, _options.Port);
        _exceptionHandler = options.ExceptionHandler;

        // Enable port reuse for faster restarts
        try
        {
            _listener.Server.SetSocketOption(
                SocketOptionLevel.Socket,
                SocketOptionName.ReuseAddress,
                true
            );
        }
        catch
        { /* Ignore if not supported */
        }
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

                // CRITICAL FIX: Ensure startup promise is always resolved
                _serverReadyTcs.TrySetException(ex);
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
            await _logger.InfoAsync(
                $"TCP server started on {_options.IpAddress}:{_options.Port} (Max connections: {_options.MaxConcurrentConnections})",
                serverToken
            );

            // Signal server is ready
            _serverReadyTcs.TrySetResult();

            while (!serverToken.IsCancellationRequested)
            {
                await _connectionSemaphore.WaitAsync(serverToken).ConfigureAwait(false);
                var client = await _listener
                    .AcceptTcpClientAsync(serverToken)
                    .ConfigureAwait(false);

                // Update connection count safely
                Interlocked.Increment(ref _totalConnections);
                var clientTask = ProcessClientAsync(client, serverToken);
                _activeTasks.TryAdd(clientTask, true);
            }
        }
        catch (OperationCanceledException)
        {
            /* Normal shutdown */
        }
        catch (Exception ex)
        {
            await _logger.CriticalAsync("Server loop fatal error", ex, CancellationToken.None);

            // CRITICAL FIX: Ensure startup promise is always resolved
            if (!_serverReadyTcs.Task.IsCompleted)
                _serverReadyTcs.TrySetException(ex);

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
            // SAFETY: Create snapshot to avoid concurrent modification
            var tasksSnapshot = _activeTasks.Keys.ToList();

            using var timeoutCts = new CancellationTokenSource(_options.ClientStopTimeout);
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken,
                timeoutCts.Token
            );

            try
            {
                await Task.WhenAll(tasksSnapshot).WaitAsync(linkedCts.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested)
            {
                await _logger.WarningAsync(
                    $"Timeout waiting for {tasksSnapshot.Count} client tasks",
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
            }
            catch (Exception ex)
            {
                _ = _logger.WarningAsync("Error stopping listener", ex);
            }
        }
    }

    private async Task ProcessClientAsync(TcpClient client, CancellationToken cancellationToken)
    {
        // Store task reference for later removal
        var currentTask = Task.CurrentId.HasValue ? Task.FromResult(true) : null;

        // SAFETY: Use safe endpoint resolution
        var remoteEndPoint = SafeGetEndpoint(client) ?? "unknown";

        try
        {
            await _logger.DebugAsync(
                $"Accepted connection from {remoteEndPoint}",
                cancellationToken
            );

            using (client)
            await using (var stream = client.GetStream())
            {
                // Create handler and ensure proper disposal
                var handler = _tcpHandlerFactory();
                try
                {
                    await handler.HandleAsync(stream, cancellationToken);
                }
                finally
                {
                    switch (handler)
                    {
                        // Support both sync and async disposable handlers
                        case IAsyncDisposable asyncDisposable:
                            await asyncDisposable.DisposeAsync();
                            break;
                        case IDisposable disposable:
                            disposable.Dispose();
                            break;
                    }
                }
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

            if (SafeGetIpEndPoint(client) is { } ipEp)
            {
                try
                {
                    _exceptionHandler?.Invoke(ex, ipEp);
                }
                catch (Exception handlerEx)
                {
                    await _logger.ErrorAsync(
                        $"Exception handler failed for {remoteEndPoint}",
                        handlerEx,
                        cancellationToken
                    );
                }
            }
        }
        finally
        {
            _connectionSemaphore.Release();

            // PRECISE TASK REMOVAL: Remove current task from tracking
            if (currentTask != null)
                _activeTasks.TryRemove(currentTask, out _);
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
                await _serverTask.WaitAsync(_options.ServerStopTimeout);
            }
            catch (TimeoutException)
            {
                await _logger.WarningAsync(
                    $"Timeout waiting for server loop after {_options.ServerStopTimeout.TotalSeconds}s"
                );
            }
        }

        // Wait for client tasks
        if (_activeTasks.Any())
        {
            try
            {
                // SAFETY: Create snapshot to avoid concurrent modification
                var tasksSnapshot = _activeTasks.Keys.ToList();
                await Task.WhenAll(tasksSnapshot)
                    .WaitAsync(_options.ClientStopTimeout)
                    .ConfigureAwait(false);
            }
            catch (TimeoutException)
            {
                await _logger.WarningAsync(
                    $"Timeout waiting for {_activeTasks.Count} client tasks after {_options.ClientStopTimeout.TotalSeconds}s"
                );
            }
        }

        _serverCts.Dispose();
        _connectionSemaphore.Dispose();
    }

    #region Helper Methods
    // Centralized endpoint formatting
    private static string? SafeGetEndpoint(TcpClient client)
    {
        try
        {
            if (client.Client.RemoteEndPoint is IPEndPoint ipEp)
                return $"{ipEp.Address}:{ipEp.Port}";
        }
        catch (ObjectDisposedException)
        {
            return "disposed-endpoint";
        }
        catch (SocketException)
        {
            return "invalid-endpoint";
        }
        return null;
    }

    private static IPEndPoint? SafeGetIpEndPoint(TcpClient client)
    {
        try
        {
            return client.Client.RemoteEndPoint as IPEndPoint;
        }
        catch
        {
            return null;
        }
    }
    #endregion
}
