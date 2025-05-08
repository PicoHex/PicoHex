namespace Pico.Svr;

public sealed class TcpServer : IDisposable
{
    private readonly IPAddress _ipAddress;
    private readonly ushort _port;
    private readonly ILogger<TcpServer> _logger;
    private readonly SemaphoreSlim _connectionSemaphore;
    private readonly TcpListener _listener;
    private readonly Func<ITcpHandler> _tcpHandlerFactory;
    private volatile bool _isDisposed;

    public TcpServer(
        IPAddress ipAddress,
        ushort port,
        Func<ITcpHandler> tcpHandlerFactory,
        ILogger<TcpServer> logger,
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
        _listener.Start();
        await _logger.InfoAsync($"TCP server started on {_ipAddress}:{_port}", cancellationToken);

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                await _connectionSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
                TcpClient? client = null;

                try
                {
                    client = await _listener
                        .AcceptTcpClientAsync(cancellationToken)
                        .ConfigureAwait(false);

                    await _logger.InfoAsync(
                        $"Accepted new client connection {client.Client.RemoteEndPoint}",
                        cancellationToken
                    );

                    // Fire and forget with proper error handling
                    _ = ProcessClientAsync(client, cancellationToken);
                }
                catch (Exception ex) when (ex is OperationCanceledException or SocketException)
                {
                    client?.Dispose();
                    _connectionSemaphore.Release();

                    if (ex is SocketException socketEx)
                    {
                        await _logger.WarningAsync(
                            "Socket exception while accepting connection",
                            socketEx,
                            cancellationToken
                        );
                    }

                    break;
                }
                catch (Exception ex)
                {
                    client?.Dispose();
                    _connectionSemaphore.Release();
                    await _logger.ErrorAsync(
                        "Error accepting client connection",
                        ex,
                        cancellationToken: cancellationToken
                    );
                }
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            await _logger.ErrorAsync(
                "TCP server fatal error",
                ex,
                cancellationToken: cancellationToken
            );
            throw;
        }
        finally
        {
            Stop();

            await _logger.InfoAsync("TCP server stopped", cancellationToken);
        }
    }

    private async Task ProcessClientAsync(TcpClient client, CancellationToken cancellationToken)
    {
        try
        {
            using (client)
            {
                await using var stream = client.GetStream();
                var handler = _tcpHandlerFactory();
                await handler.HandleAsync(stream, cancellationToken).ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            await _logger.ErrorAsync(
                "Client processing error",
                ex,
                cancellationToken: cancellationToken
            );
        }
        finally
        {
            _connectionSemaphore.Release();
            await TryLogClientDisconnect(client, cancellationToken);
        }
    }

    private async ValueTask TryLogClientDisconnect(
        TcpClient client,
        CancellationToken cancellationToken
    )
    {
        try
        {
            var endpoint = client.Client.RemoteEndPoint?.ToString() ?? "unknown";

            await _logger.InfoAsync($"Client {endpoint} disconnected", cancellationToken);
        }
        catch
        {
            // Suppress logging errors
        }
    }

    private void Stop()
    {
        if (_listener.Server.IsBound)
        {
            _listener.Stop();
        }
    }

    public void Dispose()
    {
        if (_isDisposed)
            return;

        Stop();
        _connectionSemaphore.Dispose();
        _isDisposed = true;
    }
}
