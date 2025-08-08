namespace Pico.Node;

public sealed class UdpNode : INode
{
    // Server configuration
    private readonly IPAddress _ipAddress;
    private readonly ushort _port;
    private readonly ILogger<UdpNode> _logger;
    private readonly Func<IUdpHandler> _udpHandlerFactory;
    private readonly Action<Exception, string>? _exceptionHandler;

    // Runtime state
    private readonly ConcurrentDictionary<Task, bool> _activeTasks = new();
    private volatile bool _isDisposed;
    private readonly CancellationTokenSource _serverCts = new();
    private readonly Lock _stateLock = new();
    private bool _isRunning;
    private UdpClient? _udpClient;

    public bool IsRunning
    {
        get
        {
            lock (_stateLock)
                return _isRunning;
        }
    }

    public UdpNode(
        IPAddress ipAddress,
        ushort port,
        Func<IUdpHandler> udpHandlerFactory,
        ILogger<UdpNode> logger,
        Action<Exception, string>? exceptionHandler = null
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
    }

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        lock (_stateLock)
        {
            if (_isRunning)
                return;
            _isRunning = true;
            _udpClient = new UdpClient(new IPEndPoint(_ipAddress, _port));
        }

        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken,
            _serverCts.Token
        );

        var serverToken = linkedCts.Token;
        await _logger.InfoAsync($"UDP server started on {_ipAddress}:{_port}", serverToken);

        try
        {
            while (!serverToken.IsCancellationRequested)
            {
                try
                {
                    // Receive UDP datagram asynchronously
                    var receiveResult = await _udpClient
                        .ReceiveAsync(serverToken)
                        .ConfigureAwait(false);

                    // Create and track processing task
                    var processTask = ProcessDatagramAsync(
                            receiveResult.Buffer,
                            receiveResult.RemoteEndPoint,
                            serverToken
                        )
                        .ContinueWith(
                            t =>
                            {
                                _activeTasks.TryRemove(t, out _);
                                if (t is { IsFaulted: true, Exception: not null })
                                {
                                    _logger
                                        .ErrorAsync(
                                            "Unhandled exception in UDP processing task",
                                            t.Exception,
                                            cancellationToken: serverToken
                                        )
                                        .GetAwaiter()
                                        .GetResult();
                                }
                            },
                            TaskContinuationOptions.ExecuteSynchronously
                        );

                    _activeTasks.TryAdd(processTask, true);
                }
                catch (OperationCanceledException)
                {
                    // Normal shutdown
                }
                catch (SocketException ex)
                {
                    await HandleSocketExceptionAsync(ex, serverToken);
                }
                catch (ObjectDisposedException)
                {
                    // Shutting down
                }
                catch (Exception ex)
                {
                    await _logger.ErrorAsync(
                        "UDP server receive error",
                        ex,
                        cancellationToken: serverToken
                    );
                }
            }
        }
        finally
        {
            StopServer();
            lock (_stateLock)
                _isRunning = false;
            await _logger.InfoAsync("UDP server stopped", serverToken);
        }
    }

    private async Task ProcessDatagramAsync(
        byte[] datagram,
        IPEndPoint remoteEndpoint,
        CancellationToken cancellationToken
    )
    {
        try
        {
            var handler = _udpHandlerFactory();
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
            await _logger.ErrorAsync(
                $"Error processing UDP datagram from {remoteEndpoint}",
                ex,
                cancellationToken: cancellationToken
            );

            _exceptionHandler?.Invoke(ex, remoteEndpoint.ToString());
        }
    }

    private async Task HandleSocketExceptionAsync(SocketException ex, CancellationToken token)
    {
        if (IsCriticalSocketError(ex.SocketErrorCode))
        {
            await _logger.CriticalAsync(
                $"Critical UDP socket error: {ex.SocketErrorCode}",
                ex,
                token
            );
            StopServer();
        }
        else
        {
            await _logger.WarningAsync(
                $"Non-critical UDP socket error: {ex.SocketErrorCode}",
                ex,
                token
            );
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        StopServer();

        if (!_activeTasks.IsEmpty)
        {
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

            _serverCts.Cancel();
            _udpClient?.Close();
            _udpClient = null;
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

            if (!_activeTasks.IsEmpty)
            {
                await Task.WhenAll(_activeTasks.Keys).ConfigureAwait(false);
            }
        }
        finally
        {
            if (!_isDisposed)
            {
                _udpClient?.Dispose();
                _serverCts.Dispose();
                _isDisposed = true;
            }
        }
    }

    public void Dispose()
    {
        if (_isDisposed)
            return;

        try
        {
            StopServer();

            if (!_activeTasks.IsEmpty)
            {
                Task.WaitAll(_activeTasks.Keys.ToArray(), TimeSpan.FromSeconds(5));
            }
        }
        finally
        {
            if (!_isDisposed)
            {
                _udpClient?.Dispose();
                _serverCts.Dispose();
                _isDisposed = true;
            }
        }
    }
}
