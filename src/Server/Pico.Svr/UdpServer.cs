namespace Pico.SVR;

public class UdpServer : IDisposable, IAsyncDisposable
{
    private readonly IPAddress _ipAddress;
    private readonly ushort _port;
    private readonly Func<IUdpHandler> _udpHandlerFactory;
    private readonly ILogger<UdpServer> _logger;
    private readonly ArrayPool<byte> _bufferPool;
    private readonly UdpClient _udpClient;
    private bool _isDisposed;

    public UdpServer(
        IPAddress ipAddress,
        ushort port,
        Func<IUdpHandler> udpHandlerFactory,
        ILogger<UdpServer> logger
    )
    {
        _ipAddress = ipAddress ?? throw new ArgumentNullException(nameof(ipAddress));
        _port = port;
        _udpHandlerFactory =
            udpHandlerFactory ?? throw new ArgumentNullException(nameof(udpHandlerFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _bufferPool = ArrayPool<byte>.Shared;
        _udpClient = new UdpClient(new IPEndPoint(_ipAddress, _port));
        _udpClient.EnableBroadcast = true;
    }

    /// <summary>
    /// Starts the UDP server and begins listening for incoming datagrams.
    /// </summary>
    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        await _logger.InfoAsync(
            $"UDP server started on {_ipAddress}:{_port}",
            cancellationToken: cancellationToken
        );

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var buffer = _bufferPool.Rent(65535); // Max UDP size
                try
                {
                    var result = await _udpClient
                        .ReceiveAsync(cancellationToken)
                        .ConfigureAwait(false);

                    Buffer.BlockCopy(result.Buffer, 0, buffer, 0, result.Buffer.Length);

                    _ = Task.Run(
                        () =>
                            HandleReceivedDataAsync(
                                buffer,
                                result.RemoteEndPoint,
                                cancellationToken
                            ),
                        cancellationToken
                    );
                }
                catch (ObjectDisposedException)
                {
                    // Expected when shutting down
                    break;
                }
                catch (Exception ex)
                {
                    await _logger.ErrorAsync(
                        "Error receiving UDP datagram",
                        ex,
                        cancellationToken: cancellationToken
                    );
                    _bufferPool.Return(buffer);
                }
            }
        }
        catch (OperationCanceledException)
        {
            await _logger.InfoAsync(
                "UDP server shutdown requested",
                cancellationToken: cancellationToken
            );
        }
        catch (Exception ex)
        {
            await _logger.ErrorAsync(
                "UDP server encountered an error",
                ex,
                cancellationToken: cancellationToken
            );
        }
        finally
        {
            Stop();
            await _logger.InfoAsync("UDP server stopped", cancellationToken: cancellationToken);
        }
    }

    private async Task HandleReceivedDataAsync(
        byte[] data,
        EndPoint remoteEndPoint,
        CancellationToken cancellationToken
    )
    {
        try
        {
            var udpHandler = _udpHandlerFactory();
            await udpHandler
                .HandleAsync(data, remoteEndPoint, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            await _logger.ErrorAsync(
                $"Error processing datagram from {remoteEndPoint}",
                ex,
                cancellationToken: cancellationToken
            );
        }
        finally
        {
            _bufferPool.Return(data);
        }
    }

    /// <summary>
    /// Gracefully stops the UDP server.
    /// </summary>
    private void Stop()
    {
        _udpClient.Close();
        _udpClient.Dispose();
    }

    /// <summary>
    /// Disposes the UDP server and its resources.
    /// </summary>
    public void Dispose()
    {
        if (_isDisposed)
            return;

        Stop();
        _udpClient.Dispose();
        _isDisposed = true;
    }

    public async ValueTask DisposeAsync()
    {
        await CastAndDispose(_udpClient);

        return;

        static async ValueTask CastAndDispose(IDisposable resource)
        {
            if (resource is IAsyncDisposable resourceAsyncDisposable)
                await resourceAsyncDisposable.DisposeAsync();
            else
                resource.Dispose();
        }
    }
}
