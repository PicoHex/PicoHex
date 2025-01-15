namespace PicoHex.Server;

public class UdpServer : IDisposable
{
    private readonly IPAddress _ipAddress;
    private readonly int _port;
    private readonly Func<IBytesHandler> _bytesHandlerFactory;
    private readonly ILogger<UdpServer> _logger;
    private readonly ArrayPool<byte> _bufferPool;
    private readonly UdpClient _udpClient;
    private bool _isDisposed;

    public UdpServer(
        IPAddress ipAddress,
        int port,
        Func<IBytesHandler> bytesHandlerFactory,
        ILogger<UdpServer> logger
    )
    {
        _ipAddress = ipAddress ?? throw new ArgumentNullException(nameof(ipAddress));
        _port = port;
        _bytesHandlerFactory =
            bytesHandlerFactory ?? throw new ArgumentNullException(nameof(bytesHandlerFactory));
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
        _logger.LogInformation("UDP server started on {IPAddress}:{Port}", _ipAddress, _port);

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
                    _logger.LogError(ex, "Error receiving UDP datagram");
                    _bufferPool.Return(buffer);
                }
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("UDP server shutdown requested");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "UDP server encountered an error");
        }
        finally
        {
            Stop();
            _logger.LogInformation("UDP server stopped");
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
            var bytesHandler = _bytesHandlerFactory();
            await bytesHandler
                .HandleAsync(data, remoteEndPoint, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error processing datagram from {@RemoteEndPoint}",
                remoteEndPoint
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
}
