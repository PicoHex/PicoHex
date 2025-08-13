namespace Pico.Node;

public sealed class UdpNode : INode
{
    private readonly UdpNodeOptions _options;
    private readonly ILogger<UdpNode> _logger;
    private readonly Func<IUdpHandler> _udpHandlerFactory;
    private readonly SemaphoreSlim _concurrencyLimiter;

    private volatile bool _isDisposed;
    private readonly CancellationTokenSource _serverCts = new();
    private readonly Lock _stateLock = new();
    private bool _isRunning;
    private UdpClient? _udpClient;
    private Task? _receiverTask;
    private Task? _processorTask;
    private Channel<UdpReceiveResult>? _processingQueue;
    private readonly ConcurrentDictionary<Task, bool> _activeTasks = new();

    public UdpNode(
        UdpNodeOptions options,
        Func<IUdpHandler> udpHandlerFactory,
        ILogger<UdpNode> logger
    )
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _udpHandlerFactory =
            udpHandlerFactory ?? throw new ArgumentNullException(nameof(udpHandlerFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        ValidateOptions();
        _concurrencyLimiter = new SemaphoreSlim(_options.MaxConcurrency);
    }

    private void ValidateOptions()
    {
        if (_options.IpAddress == null)
            throw new ArgumentException($"{nameof(UdpNodeOptions.IpAddress)} is required");

        if (_options.MaxConcurrency <= 0)
            throw new ArgumentException(
                $"{nameof(UdpNodeOptions.MaxConcurrency)} must be positive"
            );

        if (_options.MaxQueueSize <= 0)
            throw new ArgumentException($"{nameof(UdpNodeOptions.MaxQueueSize)} must be positive");
    }

    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        lock (_stateLock)
        {
            if (_isRunning)
                return Task.CompletedTask;

            try
            {
                _udpClient = new UdpClient(new IPEndPoint(_options.IpAddress, _options.Port));
                _options.ConfigureUdpClient?.Invoke(_udpClient);

                _processingQueue = Channel.CreateBounded<UdpReceiveResult>(
                    new BoundedChannelOptions(_options.MaxQueueSize)
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
                _logger.CriticalAsync("Failed to initialize UDP server", ex, cancellationToken);
                _udpClient?.Dispose();
                throw;
            }

            _receiverTask = RunReceiverAsync(cancellationToken);
            _processorTask = RunProcessorAsync(cancellationToken);
        }

        return Task.CompletedTask;
    }

    private async Task RunReceiverAsync(CancellationToken externalToken)
    {
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
            externalToken,
            _serverCts.Token
        );
        var serverToken = linkedCts.Token;

        await SafeLogAsync(
            () =>
                _logger.InfoAsync(
                    $"UDP receiver started on {_options.IpAddress}:{_options.Port}",
                    serverToken
                ),
            serverToken
        );

        try
        {
            while (!serverToken.IsCancellationRequested)
            {
                try
                {
                    var receiveResult = await _udpClient!.ReceiveAsync(serverToken);
                    if (
                        _processingQueue != null
                        && !_processingQueue.Writer.TryWrite(receiveResult)
                    )
                    {
                        await SafeLogAsync(
                            () =>
                                _logger.WarningAsync(
                                    "UDP processing queue full, datagram dropped",
                                    cancellationToken: serverToken
                                ),
                            serverToken
                        );
                    }
                }
                catch (OperationCanceledException) when (serverToken.IsCancellationRequested)
                {
                    break;
                }
                catch (SocketException ex)
                {
                    await HandleSocketExceptionAsync(ex, serverToken);
                }
                catch (ObjectDisposedException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    await SafeLogAsync(
                        () => _logger.ErrorAsync("UDP receiver error", ex, serverToken),
                        serverToken
                    );
                }
            }
        }
        finally
        {
            _processingQueue?.Writer.Complete();
            await SafeLogAsync(
                () => _logger.InfoAsync("UDP receiver stopped", CancellationToken.None),
                CancellationToken.None
            );
        }
    }

    private async Task RunProcessorAsync(CancellationToken externalToken)
    {
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
            externalToken,
            _serverCts.Token
        );
        var serverToken = linkedCts.Token;

        await SafeLogAsync(
            () => _logger.InfoAsync("UDP processor started", serverToken),
            serverToken
        );

        if (_processingQueue == null)
            return;

        try
        {
            await foreach (var result in _processingQueue.Reader.ReadAllAsync(serverToken))
            {
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

                _activeTasks.TryAdd(processTask, true);
                _ = processTask.ContinueWith(
                    t => _activeTasks.TryRemove(t, out _),
                    TaskContinuationOptions.ExecuteSynchronously
                );
            }
        }
        catch (OperationCanceledException)
        { /* Expected */
        }
        catch (Exception ex)
        {
            await SafeLogAsync(
                () => _logger.ErrorAsync("UDP processor error", ex, serverToken),
                serverToken
            );
        }
        finally
        {
            await SafeLogAsync(
                () => _logger.InfoAsync("UDP processor stopped", CancellationToken.None),
                CancellationToken.None
            );
        }
    }

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
            await handler.HandleAsync(datagram, remoteEndpoint, cancellationToken);
        }
        catch (OperationCanceledException)
        { /* Normal */
        }
        catch (Exception ex)
        {
            await SafeLogAsync(
                () =>
                    _logger.ErrorAsync(
                        $"Error processing UDP datagram from {remoteEndpoint}",
                        ex,
                        cancellationToken
                    ),
                cancellationToken
            );

            try
            {
                _options.ExceptionHandler?.Invoke(ex, remoteEndpoint);
            }
            catch (Exception handlerEx)
            {
                await SafeLogAsync(
                    () =>
                        _logger.ErrorAsync(
                            "Exception handler failed",
                            handlerEx,
                            cancellationToken
                        ),
                    cancellationToken
                );
            }
        }
        finally
        {
            switch (handler)
            {
                case IDisposable disposable:
                    disposable.Dispose();
                    break;
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
                () =>
                    _logger.CriticalAsync(
                        $"Critical UDP socket error: {ex.SocketErrorCode}",
                        ex,
                        token
                    ),
                token
            );
            StopServer();
        }
        else
        {
            await SafeLogAsync(
                () =>
                    _logger.WarningAsync(
                        $"Non-critical UDP socket error: {ex.SocketErrorCode}",
                        ex,
                        token
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
                await Task.WhenAll(_activeTasks.Keys)
                    .WaitAsync(_options.StopTasksTimeout, cancellationToken);
            }
            catch (TimeoutException)
            {
                await SafeLogAsync(
                    () =>
                        _logger.WarningAsync(
                            "Timeout waiting for tasks during shutdown",
                            cancellationToken: cancellationToken
                        ),
                    CancellationToken.None
                );
            }
            catch (OperationCanceledException)
            { /* External cancellation */
            }
            catch (AggregateException ae)
            {
                await SafeLogAsync(
                    () =>
                        _logger.ErrorAsync(
                            "Errors during task shutdown",
                            ae.Flatten(),
                            cancellationToken
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

            _serverCts.Cancel();
            _isRunning = false;

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

    public async ValueTask DisposeAsync()
    {
        if (_isDisposed)
            return;

        try
        {
            StopServer();

            await SafeWaitForTaskAsync(
                _receiverTask,
                "receiver",
                _options.DisposeServerTaskTimeout
            );
            await SafeWaitForTaskAsync(
                _processorTask,
                "processor",
                _options.DisposeServerTaskTimeout
            );

            if (!_activeTasks.IsEmpty)
            {
                try
                {
                    await Task.WhenAll(_activeTasks.Keys)
                        .WaitAsync(_options.DisposeProcessingTasksTimeout);
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

    private async Task SafeWaitForTaskAsync(Task? task, string taskName, TimeSpan timeout)
    {
        if (task == null)
            return;

        try
        {
            await task.WaitAsync(timeout);
        }
        catch (TimeoutException)
        {
            await _logger.WarningAsync($"Timeout waiting for {taskName} task during disposal");
        }
        catch (Exception ex)
        {
            await _logger.WarningAsync($"{taskName} task error during disposal", ex);
        }
    }

    private async ValueTask SafeLogAsync(
        Func<ValueTask> logAction,
        CancellationToken cancellationToken
    )
    {
        if (cancellationToken.IsCancellationRequested)
            return;

        try
        {
            await logAction();
        }
        catch (Exception ex)
        {
            try
            {
                await _logger.WarningAsync("Failed to write log message", ex, cancellationToken);
            }
            catch
            { /* Final fallback - ignore */
            }
        }
    }

    private static bool IsCriticalSocketError(SocketError error) =>
        error switch
        {
            SocketError.AccessDenied
            or SocketError.AddressAlreadyInUse
            or SocketError.AddressNotAvailable
            or SocketError.InvalidArgument
            or SocketError.Shutdown
            or SocketError.ConnectionReset
                => true,
            _ => false
        };
}
