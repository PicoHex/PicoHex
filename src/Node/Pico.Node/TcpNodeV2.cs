namespace Pico.Node;

/// <summary>
/// A high-performance TCP server built on a producer-consumer architecture using System.Threading.Channels
/// and System.IO.Pipelines. This design decouples connection acceptance from processing, providing
/// robust backpressure and efficient resource management.
/// </summary>
public sealed class TcpNodeV2 : INode
{
    // Dependencies and Configuration
    private readonly TcpNodeOptionsV2 _options;
    private readonly ILogger<TcpNodeV2> _logger;
    private readonly Func<IPipelineHandler> _handlerFactory;
    private readonly TcpListener _listener;

    // State Management
    private readonly CancellationTokenSource _serverCts = new();
    private readonly Lock _stateLock = new();
    private bool _isRunning;
    private bool _isDisposed;

    // Core Architecture Components
    private Channel<TcpClient>? _connectionChannel;
    private Task? _acceptorTask;
    private readonly List<Task> _workerTasks = [];

    public TcpNodeV2(
        TcpNodeOptionsV2 options,
        Func<IPipelineHandler> handlerFactory,
        ILogger<TcpNodeV2> logger
    )
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _handlerFactory = handlerFactory ?? throw new ArgumentNullException(nameof(handlerFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        _listener = new TcpListener(_options.IpAddress, _options.Port);

        // Enable port reuse for faster restarts, a professional touch.
        try
        {
            _listener.Server.SetSocketOption(
                SocketOptionLevel.Socket,
                SocketOptionName.ReuseAddress,
                true
            );
        }
        catch (Exception ex)
        {
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

                _listener.Start();
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
                // Asynchronously accept a client and wait to write it to the channel.
                // If the channel is full, this await will pause, providing backpressure.
                var client = await _listener.AcceptTcpClientAsync(cancellationToken);
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
        using (client)
        {
            var remoteEndPoint = SafeGetIpEndPoint(client);
            await _logger.DebugAsync(
                $"Processing connection from: {remoteEndPoint}",
                cancellationToken: cancellationToken
            );

            var stream = client.GetStream();
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
            StopServer();
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

    private void StopServer()
    {
        // This private method contains the non-async part of the shutdown.
        // It can be called safely from StopAsync and DisposeAsync.
        lock (_stateLock)
        {
            if (!_isRunning)
                return;

            _isRunning = false;
            _serverCts.Cancel();
            _listener.Stop();
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

        await StopAsync(CancellationToken.None);

        _serverCts.Dispose();
        // The TcpListener itself does not implement IDisposable, but its underlying socket does.
        // Stop() is sufficient to release the socket.
    }

    private static IPEndPoint? SafeGetIpEndPoint(TcpClient client)
    {
        try
        {
            return client.Client.RemoteEndPoint as IPEndPoint;
        }
        catch
        {
            // Can throw if the socket is already closed.
            return null;
        }
    }
}
