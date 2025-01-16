using PicoHex.Server.Abstractions;

namespace PicoHex.Server;

public class TcpServer : IDisposable
{
    private readonly IPAddress _ipAddress;
    private readonly int _port;
    private readonly ILogger<TcpServer> _logger;
    private readonly SemaphoreSlim _connectionSemaphore;
    private readonly TcpListener _listener;
    private bool _isDisposed;
    private readonly Func<ITcpHandler> _tcpHandlerFactory;

    public TcpServer(
        IPAddress ipAddress,
        int port,
        Func<ITcpHandler>? tcpHandlerFactory,
        ILogger<TcpServer>? logger,
        int maxConcurrentConnections = 100
    )
    {
        _ipAddress = ipAddress ?? throw new ArgumentNullException(nameof(ipAddress));
        _port = port;
        _tcpHandlerFactory =
            tcpHandlerFactory ?? throw new ArgumentNullException(nameof(tcpHandlerFactory));
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

                try
                {
                    var client = await _listener
                        .AcceptTcpClientAsync(cancellationToken)
                        .ConfigureAwait(false);
                    if (cancellationToken.IsCancellationRequested)
                    {
                        _connectionSemaphore.Release();
                        break;
                    }

                    _ = HandleClientConnectionAsync(client, cancellationToken)
                        .ContinueWith(_ => _connectionSemaphore.Release(), TaskScheduler.Default);
                }
                catch (SocketException socketEx)
                {
                    _logger.LogWarning(
                        socketEx,
                        "Socket exception while accepting a client connection."
                    );
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Unexpected error while accepting a client connection.");
                }
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
                // Read the initial HTTP request line to get the path
                var reader = new StreamReader(stream, Encoding.UTF8, leaveOpen: true);
                var requestLine = await reader.ReadLineAsync(cancellationToken);
                if (string.IsNullOrWhiteSpace(requestLine))
                    throw new InvalidOperationException("Empty request line");

                var parts = requestLine.Split(' ');
                if (parts.Length < 2)
                    throw new InvalidOperationException("Invalid request line");

                // Get the appropriate handler from the factory
                var handler = _tcpHandlerFactory();

                // Handle the request
                await handler.HandleAsync(stream, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling client connection");
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
