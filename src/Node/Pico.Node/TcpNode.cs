namespace Pico.Node;

public sealed class TcpNode : IDisposable
{
    private readonly IPAddress _ipAddress;
    private readonly ushort _port;
    private readonly ILogger<TcpNode> _logger;
    private readonly SemaphoreSlim _connectionSemaphore;
    private readonly TcpListener _listener;
    private readonly Func<ITcpHandler> _tcpHandlerFactory;
    private readonly ConcurrentBag<Task> _activeTasks = [];
    private volatile bool _isDisposed;
    private readonly CancellationTokenSource _serverCts = new();

    public TcpNode(
        IPAddress ipAddress,
        ushort port,
        Func<ITcpHandler> tcpHandlerFactory,
        ILogger<TcpNode> logger,
        int maxConcurrentConnections = 100
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
    }

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        // Create linked cancellation token to support both external and internal cancellation
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
                // Wait for available connection slot
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

                    // Start client processing task and track it
                    var clientTask = ProcessClientAsync(client, serverToken);
                    _activeTasks.Add(clientTask);

                    // Remove task from tracking when completed
                    await clientTask.ContinueWith(
                        t =>
                        {
                            _activeTasks.TryTake(out _);
                        },
                        TaskContinuationOptions.ExecuteSynchronously
                    );
                }
                catch (Exception ex) when (ex is OperationCanceledException)
                {
                    // Ensure client disposal on cancellation
                    client?.Dispose();
                    _connectionSemaphore.Release();
                    break; // Normal cancellation
                }
                catch (SocketException socketEx)
                {
                    // Ensure client disposal on socket errors
                    client?.Dispose();
                    _connectionSemaphore.Release();
                    await _logger.WarningAsync(
                        "Socket exception while accepting connection",
                        socketEx,
                        serverToken
                    );
                    // Stop server on critical socket errors
                    if (IsCriticalSocketError(socketEx.SocketErrorCode))
                    {
                        break;
                    }
                }
                catch (Exception ex)
                {
                    // Ensure client disposal on general errors
                    client?.Dispose();
                    _connectionSemaphore.Release();
                    await _logger.ErrorAsync(
                        "Error accepting client connection",
                        ex,
                        cancellationToken: serverToken
                    );
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
            Stop();
            await _logger.InfoAsync("TCP server stopped", serverToken);
        }
    }

    private async Task ProcessClientAsync(TcpClient client, CancellationToken cancellationToken)
    {
        string? endpoint = null;
        try
        {
            // Capture client endpoint for logging
            endpoint = client.Client.RemoteEndPoint?.ToString() ?? "unknown";

            using (client)
            {
                await using var stream = client.GetStream();
                var handler = _tcpHandlerFactory();
                await handler.HandleAsync(stream, cancellationToken).ConfigureAwait(false);
            }

            // Log normal disconnection
            await _logger.InfoAsync($"Client {endpoint} disconnected", cancellationToken);
        }
        catch (OperationCanceledException)
        {
            // Special handling for operation cancellation
            await _logger.InfoAsync(
                $"Client {endpoint} disconnected (canceled)",
                cancellationToken
            );
        }
        catch (Exception ex)
        {
            // Log client processing errors with endpoint context
            await _logger.ErrorAsync(
                $"Client {endpoint} processing error",
                ex,
                cancellationToken: cancellationToken
            );
        }
        finally
        {
            // Release connection slot regardless of outcome
            _connectionSemaphore.Release();
        }
    }

    private static bool IsCriticalSocketError(SocketError error) =>
        error switch
        {
            SocketError.AccessDenied
            or SocketError.AddressAlreadyInUse
            or SocketError.AddressNotAvailable
            or SocketError.InvalidArgument
                => true,
            _ => false
        };

    private void Stop()
    {
        // Stop listening if server is bound
        if (_listener.Server.IsBound)
        {
            _listener.Stop();
        }

        // Signal cancellation to all client handlers
        _serverCts.Cancel();
    }

    public async Task StopAsync(TimeSpan timeout)
    {
        // Initiate server shutdown
        Stop();

        // Wait for active client tasks to complete
        if (!_activeTasks.IsEmpty)
        {
            await Task.WhenAll(_activeTasks).WaitAsync(timeout).ConfigureAwait(false);
        }
    }

    public void Dispose()
    {
        if (_isDisposed)
            return;

        try
        {
            // Ensure server is stopped
            Stop();

            // Allow up to 5 seconds for active tasks to complete
            Task.WaitAll(_activeTasks.ToArray(), TimeSpan.FromSeconds(5));
        }
        finally
        {
            // Clean up managed resources
            _connectionSemaphore.Dispose();
            _serverCts.Dispose();
            _isDisposed = true;
        }
    }
}
