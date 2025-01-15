namespace PicoHex.Server;

public class TcpServer : IDisposable
{
    private readonly IPAddress _ipAddress;
    private readonly int _port;
    private readonly Func<IStreamHandler> _streamHandlerFactory;
    private readonly ILogger<TcpServer> _logger;
    private readonly SemaphoreSlim _connectionSemaphore;
    private readonly TcpListener _listener;
    private bool _isDisposed;

    public TcpServer(
        IPAddress ipAddress,
        int port,
        Func<IStreamHandler> streamHandlerFactory,
        ILogger<TcpServer> logger,
        int maxConcurrentConnections = 100
    )
    {
        _ipAddress = ipAddress ?? throw new ArgumentNullException(nameof(ipAddress));
        _port = port;
        _streamHandlerFactory =
            streamHandlerFactory ?? throw new ArgumentNullException(nameof(streamHandlerFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _connectionSemaphore = new SemaphoreSlim(maxConcurrentConnections);
        _listener = new TcpListener(_ipAddress, _port);
    }

    /// <summary>
    /// Starts the TCP server and begins listening for client connections.
    /// </summary>
    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        _listener.Start();
        _logger.LogInformation("TCP server started on {IPAddress}:{Port}", _ipAddress, _port);

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                await _connectionSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);

                var client = await _listener
                    .AcceptTcpClientAsync(cancellationToken)
                    .ConfigureAwait(false);

                if (cancellationToken.IsCancellationRequested)
                {
                    _connectionSemaphore.Release();
                    break;
                }

                client.NoDelay = true;
                _logger.LogInformation(
                    "Client connected from {@RemoteEndPoint}",
                    client.Client.RemoteEndPoint
                );

                _ = HandleClientConnectionAsync(client, cancellationToken)
                    .ContinueWith(_ => _connectionSemaphore.Release(), TaskScheduler.Default);
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("TCP server shutdown requested");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "TCP server encountered an error");
        }
        finally
        {
            Stop();
            _logger.LogInformation("TCP server stopped");
        }
    }

    /// <summary>
    /// Gracefully stops the TCP server.
    /// </summary>
    private void Stop()
    {
        if (!_listener.Server.IsBound)
            return;

        _listener.Stop();
    }

    private async Task HandleClientConnectionAsync(
        TcpClient client,
        CancellationToken cancellationToken
    )
    {
        using (client)
        {
            await using var stream = client.GetStream();
            try
            {
                var streamHandler = _streamHandlerFactory();
                await streamHandler.HandleAsync(stream, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Error handling client {@RemoteEndPoint}",
                    client.Client.RemoteEndPoint
                );
            }
            finally
            {
                _logger.LogInformation(
                    "Client {@RemoteEndPoint} connection closed",
                    client.Client.RemoteEndPoint
                );
            }
        }
    }

    /// <summary>
    /// Disposes the TCP server and its resources.
    /// </summary>
    public void Dispose()
    {
        if (_isDisposed)
            return;

        _listener.Stop();
        _connectionSemaphore.Dispose();
        _isDisposed = true;
    }
}
