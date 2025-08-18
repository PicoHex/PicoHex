namespace Pico.Node;

/// <summary>
/// A high-performance UDP server node that orchestrates receiving and processing datagrams.
/// It uses a UdpListener for data production and a bounded channel with a semaphore for consumption.
/// </summary>
public sealed class UdpNode : INode
{
    private readonly UdpNodeOptions _options;
    private readonly ILogger<UdpNode> _logger;
    private readonly Func<IUdpHandler> _udpHandlerFactory;
    private readonly SemaphoreSlim _concurrencyLimiter;

    private UdpListener? _listener;

    private volatile bool _isDisposed;
    private readonly CancellationTokenSource _serverCts = new();
    private readonly Lock _stateLock = new();
    private bool _isRunning;

    private Task? _receiverTask;
    private Task? _processorTask;
    private Channel<PooledUdpMessage>? _processingQueue;
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
        if (_options.ReceiveBufferSize <= 0)
            throw new ArgumentException(
                $"{nameof(UdpNodeOptions.ReceiveBufferSize)} must be positive"
            );
    }

    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        lock (_stateLock)
        {
            if (_isRunning)
                return Task.CompletedTask;

            try
            {
                // Create the UdpListener which will handle all socket operations.
                _listener = new UdpListener(_options, _logger);

                _processingQueue = Channel.CreateBounded<PooledUdpMessage>(
                    new BoundedChannelOptions(_options.MaxQueueSize)
                    {
                        FullMode = BoundedChannelFullMode.DropOldest,
                        SingleWriter = true
                    }
                );
                _isRunning = true;
            }
            catch (Exception ex)
            {
                _logger.CriticalAsync("Failed to initialize UdpNode", ex, cancellationToken);
                _listener?.DisposeAsync().AsTask().Wait(TimeSpan.FromSeconds(1), cancellationToken); // Best effort cleanup
                throw;
            }

            _receiverTask = RunReceiverAsync(cancellationToken);
            _processorTask = RunProcessorAsync(cancellationToken);
        }
        return Task.CompletedTask;
    }

    /// <summary>
    /// The "Producer" part of the pipeline. Consumes the async stream from UdpListener
    /// and writes messages into the processing channel.
    /// </summary>
    private async Task RunReceiverAsync(CancellationToken externalToken)
    {
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
            externalToken,
            _serverCts.Token
        );
        var serverToken = linkedCts.Token;

        await _logger.InfoAsync("UdpNode message producer started", serverToken);

        try
        {
            // Consume the async stream of messages from the listener.
            await foreach (var message in _listener!.ListenAsync(serverToken))
            {
                if (_processingQueue!.Writer.TryWrite(message))
                    continue;
                // If the queue is full, the message is dropped.
                // We must dispose it to return the buffer to the pool.
                message.Dispose();

                await _logger.WarningAsync(
                    "UDP processing queue full; datagram dropped",
                    cancellationToken: serverToken
                );
            }
        }
        catch (OperationCanceledException)
        { /* Normal shutdown */
        }
        catch (Exception ex)
        {
            await _logger.CriticalAsync(
                "UdpNode message producer failed critically",
                ex,
                serverToken
            );
            StopServer(); // Trigger a shutdown of the entire node
        }
        finally
        {
            _processingQueue?.Writer.Complete();
            await _logger.InfoAsync("UdpNode message producer stopped", CancellationToken.None);
        }
    }

    /// <summary>
    /// The "Consumer" part of the pipeline. Reads from the channel and dispatches
    /// messages to concurrent processing tasks.
    /// </summary>
    private async Task RunProcessorAsync(CancellationToken externalToken)
    {
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
            externalToken,
            _serverCts.Token
        );
        var serverToken = linkedCts.Token;

        await _logger.InfoAsync("UdpNode processor started", serverToken);
        if (_processingQueue == null)
            return;

        try
        {
            await foreach (var message in _processingQueue.Reader.ReadAllAsync(serverToken))
            {
                if (serverToken.IsCancellationRequested)
                {
                    message.Dispose(); // Ensure unprocessed messages are disposed
                    break;
                }

                var processTask = Task.Run(
                    async () =>
                    {
                        try
                        {
                            await _concurrencyLimiter.WaitAsync(serverToken);
                            await ProcessDatagramAsync(message, serverToken);
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
            await _logger.ErrorAsync("UdpNode processor error", ex, serverToken);
        }
        finally
        {
            await _logger.InfoAsync("UdpNode processor stopped", CancellationToken.None);
        }
    }

    /// <summary>
    /// Processes a single datagram by invoking the provided handler.
    /// Ensures the message buffer is returned to the pool.
    /// </summary>
    private async Task ProcessDatagramAsync(
        PooledUdpMessage message,
        CancellationToken cancellationToken
    )
    {
        // The 'using' statement ensures message.Dispose() is called, returning the buffer to the pool.
        using (message)
        {
            IUdpHandler? handler = null;
            try
            {
                handler = _udpHandlerFactory();
                await handler.HandleAsync(message, cancellationToken);
            }
            catch (OperationCanceledException)
            { /* Normal */
            }
            catch (Exception ex)
            {
                await _logger.ErrorAsync(
                    $"Error processing datagram from {message.RemoteEndPoint}",
                    ex,
                    cancellationToken
                );
                try
                {
                    _options.ExceptionHandler?.Invoke(ex, message.RemoteEndPoint);
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
                await _logger.WarningAsync(
                    "Timeout waiting for tasks during shutdown",
                    cancellationToken: cancellationToken
                );
            }
            catch (OperationCanceledException)
            { /* External cancellation */
            }
            catch (AggregateException ae)
            {
                await _logger.ErrorAsync(
                    "Errors during task shutdown",
                    ae.Flatten(),
                    cancellationToken
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

            _serverCts.Cancel(); // This cancellation signal propagates to the listener and processor.
            _isRunning = false;
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

            if (_listener != null)
            {
                await _listener.DisposeAsync();
            }

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
}
