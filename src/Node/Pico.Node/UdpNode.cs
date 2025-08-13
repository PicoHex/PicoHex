namespace Pico.Node;

public sealed class UdpNode : INode
{
    // Configuration
    private readonly IPAddress _ipAddress;
    private readonly ushort _port;
    private readonly ILogger<UdpNode> _logger;
    private readonly Func<IUdpHandler> _udpHandlerFactory;
    private readonly Action<Exception, IPEndPoint>? _exceptionHandler;
    private readonly Action<UdpClient>? _configureUdpClient;
    private readonly SemaphoreSlim _concurrencyLimiter;
    private readonly int _maxQueueSize;

    // Runtime state
    private readonly ConcurrentDictionary<Task, bool> _activeTasks = new();
    private volatile bool _isDisposed;
    private readonly CancellationTokenSource _serverCts = new();
    private readonly Lock _stateLock = new();
    private bool _isRunning;
    private UdpClient? _udpClient;
    private Task? _receiverTask;
    private Task? _processorTask;
    private Channel<UdpReceiveResult>? _processingQueue;

    // Timeout constants
    private static readonly TimeSpan StopTasksTimeout = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan DisposeServerTaskTimeout = TimeSpan.FromSeconds(3);
    private static readonly TimeSpan DisposeProcessingTasksTimeout = TimeSpan.FromSeconds(5);

    public UdpNode(
        IPAddress ipAddress,
        ushort port,
        Func<IUdpHandler> udpHandlerFactory,
        ILogger<UdpNode> logger,
        Action<Exception, IPEndPoint>? exceptionHandler = null,
        int maxConcurrency = 1000,
        Action<UdpClient>? configureUdpClient = null,
        int maxQueueSize = 5000
    )
    {
        ArgumentNullException.ThrowIfNull(ipAddress);
        ArgumentNullException.ThrowIfNull(udpHandlerFactory);
        ArgumentNullException.ThrowIfNull(logger);

        _ipAddress = ipAddress;
        _port = port;
        _udpHandlerFactory = udpHandlerFactory;
        _logger = logger;
        _exceptionHandler = exceptionHandler;
        _configureUdpClient = configureUdpClient;
        _concurrencyLimiter = new SemaphoreSlim(maxConcurrency);
        _maxQueueSize = maxQueueSize;
    }

    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        lock (_stateLock)
        {
            if (_isRunning)
                return Task.CompletedTask;

            try
            {
                // Initialize UDP client and processing queue
                _udpClient = new UdpClient(new IPEndPoint(_ipAddress, _port));
                _configureUdpClient?.Invoke(_udpClient);

                // Create bounded channel to prevent unlimited memory growth
                _processingQueue = Channel.CreateBounded<UdpReceiveResult>(
                    new BoundedChannelOptions(_maxQueueSize)
                    {
                        FullMode = BoundedChannelFullMode.DropOldest,
                        SingleReader = false,
                        SingleWriter = true
                    }
                );

                _isRunning = true;
            }
            catch (Exception ex)
            {
                _logger.CriticalAsync(
                    "Failed to initialize UDP server",
                    ex,
                    cancellationToken: cancellationToken
                );
                _udpClient?.Dispose();
                throw;
            }

            // Start receiver and processor in background
            _receiverTask = RunReceiverAsync(cancellationToken);
            _processorTask = RunProcessorAsync(cancellationToken);
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Runs the receiver loop to accept incoming datagrams
    /// </summary>
    private async Task RunReceiverAsync(CancellationToken externalToken)
    {
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
            externalToken,
            _serverCts.Token
        );
        var serverToken = linkedCts.Token;

        await SafeLogAsync(
            async () =>
                await _logger.InfoAsync(
                    $"UDP receiver started on {_ipAddress}:{_port}",
                    cancellationToken: serverToken
                ),
            serverToken
        );

        try
        {
            while (!serverToken.IsCancellationRequested)
            {
                try
                {
                    var receiveResult = await _udpClient!
                        .ReceiveAsync(serverToken)
                        .ConfigureAwait(false);

                    // Try adding to queue with backpressure handling
                    if (
                        _processingQueue != null
                        && !_processingQueue.Writer.TryWrite(receiveResult)
                    )
                    {
                        await SafeLogAsync(
                            async () =>
                                await _logger.WarningAsync(
                                    "UDP processing queue full, datagram dropped",
                                    cancellationToken: serverToken
                                ),
                            serverToken
                        );
                    }
                }
                catch (OperationCanceledException) when (serverToken.IsCancellationRequested)
                {
                    // Normal shutdown
                    break;
                }
                catch (SocketException ex)
                {
                    await HandleSocketExceptionAsync(ex, serverToken);
                }
                catch (ObjectDisposedException)
                {
                    break; // Server shutdown
                }
                catch (Exception ex)
                {
                    await SafeLogAsync(
                        async () =>
                            await _logger.ErrorAsync(
                                "UDP receiver error",
                                ex,
                                cancellationToken: serverToken
                            ),
                        serverToken
                    );
                }
            }
        }
        finally
        {
            // Signal end of processing
            _processingQueue?.Writer.Complete();
            await SafeLogAsync(
                async () =>
                    await _logger.InfoAsync(
                        "UDP receiver stopped",
                        cancellationToken: CancellationToken.None
                    ),
                CancellationToken.None
            );
        }
    }

    /// <summary>
    /// Runs the processor loop to handle datagrams from the queue
    /// </summary>
    private async Task RunProcessorAsync(CancellationToken externalToken)
    {
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
            externalToken,
            _serverCts.Token
        );
        var serverToken = linkedCts.Token;

        await SafeLogAsync(
            async () =>
                await _logger.InfoAsync("UDP processor started", cancellationToken: serverToken),
            serverToken
        );

        if (_processingQueue == null)
            return;

        try
        {
            await foreach (var result in _processingQueue.Reader.ReadAllAsync(serverToken))
            {
                // Skip processing if shutdown requested
                if (serverToken.IsCancellationRequested)
                    break;

                var processTask = Task.Run(
                    async () =>
                    {
                        try
                        {
                            await _concurrencyLimiter.WaitAsync(serverToken);
                            await ProcessDatagramAsync(
                                result.Buffer,
                                result.RemoteEndPoint,
                                serverToken
                            );
                        }
                        finally
                        {
                            _concurrencyLimiter.Release();
                        }
                    },
                    serverToken
                );

                // Track active tasks
                _activeTasks.TryAdd(processTask, true);
                _ = processTask.ContinueWith(
                    t => _activeTasks.TryRemove(t, out _),
                    TaskContinuationOptions.ExecuteSynchronously
                );
            }
        }
        catch (OperationCanceledException)
        {
            // Expected during shutdown
        }
        catch (Exception ex)
        {
            await SafeLogAsync(
                async () =>
                    await _logger.ErrorAsync(
                        "UDP processor error",
                        ex,
                        cancellationToken: serverToken
                    ),
                serverToken
            );
        }
        finally
        {
            await SafeLogAsync(
                async () =>
                    await _logger.InfoAsync(
                        "UDP processor stopped",
                        cancellationToken: CancellationToken.None
                    ),
                CancellationToken.None
            );
        }
    }

    /// <summary>
    /// Processes a single datagram with resource-safe handler invocation
    /// </summary>
    private async Task ProcessDatagramAsync(
        byte[] datagram,
        IPEndPoint remoteEndpoint,
        CancellationToken cancellationToken
    )
    {
        IUdpHandler? handler = null;
        try
        {
            handler = _udpHandlerFactory();
            await handler
                .HandleAsync(datagram, remoteEndpoint, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Normal cancellation
        }
        catch (Exception ex)
        {
            await SafeLogAsync(
                async () =>
                    await _logger.ErrorAsync(
                        $"Error processing UDP datagram from {remoteEndpoint}",
                        ex,
                        cancellationToken: cancellationToken
                    ),
                cancellationToken
            );

            try
            {
                _exceptionHandler?.Invoke(ex, remoteEndpoint);
            }
            catch (Exception handlerEx)
            {
                await SafeLogAsync(
                    async () =>
                        await _logger.ErrorAsync(
                            "Exception handler failed",
                            handlerEx,
                            cancellationToken: cancellationToken
                        ),
                    cancellationToken
                );
            }
        }
        finally
        {
            switch (handler)
            {
                // Dispose handler if implement IDisposable/IAsyncDisposable
                // ReSharper disable once SuspiciousTypeConversion.Global
                case IDisposable disposable:
                    disposable.Dispose();
                    break;
                // ReSharper disable once SuspiciousTypeConversion.Global
                case IAsyncDisposable asyncDisposable:
                    await asyncDisposable.DisposeAsync();
                    break;
            }
        }
    }

    private async Task HandleSocketExceptionAsync(SocketException ex, CancellationToken token)
    {
        if (IsCriticalSocketError(ex.SocketErrorCode))
        {
            await SafeLogAsync(
                async () =>
                    await _logger.CriticalAsync(
                        $"Critical UDP socket error: {ex.SocketErrorCode}",
                        ex,
                        cancellationToken: token
                    ),
                token
            );
            StopServer();
        }
        else
        {
            await SafeLogAsync(
                async () =>
                    await _logger.WarningAsync(
                        $"Non-critical UDP socket error: {ex.SocketErrorCode}",
                        ex,
                        cancellationToken: token
                    ),
                token
            );
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        StopServer();

        if (!_activeTasks.IsEmpty)
        {
            try
            {
                var completionTask = Task.WhenAll(_activeTasks.Keys);
                await completionTask.WaitAsync(StopTasksTimeout, cancellationToken);
            }
            catch (TimeoutException)
            {
                await SafeLogAsync(
                    async () =>
                        await _logger.WarningAsync(
                            "Timeout waiting for tasks during shutdown",
                            cancellationToken: cancellationToken
                        ),
                    CancellationToken.None
                );
            }
            catch (OperationCanceledException)
            {
                // External cancellation
            }
            catch (AggregateException ae)
            {
                await SafeLogAsync(
                    async () =>
                        await _logger.ErrorAsync(
                            "Errors during task shutdown",
                            ae.Flatten(),
                            cancellationToken: cancellationToken
                        ),
                    CancellationToken.None
                );
            }
        }
    }

    private void StopServer()
    {
        lock (_stateLock)
        {
            if (!_isRunning)
                return;

            // Signal shutdown
            _serverCts.Cancel();
            _isRunning = false;

            // Close UDP client
            try
            {
                _udpClient?.Close();
            }
            catch (Exception ex)
            {
                _logger.WarningAsync("Error closing UDP client", ex);
            }
            finally
            {
                _udpClient = null;
            }
        }
    }

    private static bool IsCriticalSocketError(SocketError error) =>
        error switch
        {
            SocketError.AccessDenied => true,
            SocketError.AddressAlreadyInUse => true,
            SocketError.AddressNotAvailable => true,
            SocketError.InvalidArgument => true,
            SocketError.Shutdown => true,
            SocketError.ConnectionReset => true,
            _ => false
        };

    public async ValueTask DisposeAsync()
    {
        if (_isDisposed)
            return;

        try
        {
            StopServer();

            // Wait for receiver/processor tasks
            if (_receiverTask != null)
            {
                try
                {
                    await _receiverTask.WaitAsync(DisposeServerTaskTimeout).ConfigureAwait(false);
                }
                catch (TimeoutException)
                {
                    await _logger.WarningAsync("Timeout waiting for receiver task during disposal");
                }
                catch (OperationCanceledException) { }
                catch (Exception ex)
                {
                    await _logger.WarningAsync("Receiver task error during disposal", ex);
                }
            }

            if (_processorTask != null)
            {
                try
                {
                    await _processorTask.WaitAsync(DisposeServerTaskTimeout).ConfigureAwait(false);
                }
                catch (TimeoutException)
                {
                    await _logger.WarningAsync(
                        "Timeout waiting for processor task during disposal"
                    );
                }
                catch (Exception ex)
                {
                    await _logger.WarningAsync("Processor task error during disposal", ex);
                }
            }

            // Wait for active processing tasks
            if (!_activeTasks.IsEmpty)
            {
                try
                {
                    await Task.WhenAll(_activeTasks.Keys)
                        .WaitAsync(DisposeProcessingTasksTimeout)
                        .ConfigureAwait(false);
                }
                catch (TimeoutException)
                {
                    await _logger.WarningAsync(
                        "Timeout waiting for processing tasks during disposal"
                    );
                }
                catch (AggregateException ae)
                {
                    await _logger.WarningAsync(
                        "Processing task errors during disposal",
                        ae.Flatten()
                    );
                }
            }
        }
        finally
        {
            lock (_stateLock)
            {
                if (!_isDisposed)
                {
                    _udpClient?.Dispose();
                    _serverCts.Dispose();
                    _concurrencyLimiter.Dispose();
                    _isDisposed = true;
                }
            }
        }
    }

    /// <summary>
    /// Safely executes logging operations with fallback handling
    /// </summary>
    private async Task SafeLogAsync(Func<Task> logAction, CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
            return;

        try
        {
            await logAction().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            try
            {
                // Attempt synchronous fallback
                await _logger.WarningAsync("Failed to write log message", ex, cancellationToken);
            }
            catch
            {
                // Final fallback - ignore
            }
        }
    }
}
