namespace Pico.Node;

/// <summary>
/// Encapsulates low-level UDP socket operations and exposes received datagrams as an asynchronous stream.
/// This class is responsible for the lifecycle of the socket.
/// </summary>
public sealed class UdpListener(UdpNodeOptions options, ILogger logger) : IAsyncDisposable
{
    private readonly ILogger _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    private readonly IPEndPoint _localEndPoint = new(options.IpAddress, options.Port);
    private readonly int _receiveBufferSize = options.ReceiveBufferSize;
    private readonly Action<Socket>? _configureSocket = options.ConfigureSocket;
    private Socket? _socket;

    /// <summary>
    /// Starts listening and yields received UDP messages as an asynchronous stream.
    /// </summary>
    /// <param name="cancellationToken">A cancellation token to stop the listener.</param>
    /// <returns>An async enumerable stream of messages that can be consumed with 'await foreach'.</returns>
    public async IAsyncEnumerable<PooledUdpMessage> ListenAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken = default
    )
    {
        // 1. Initialize and bind the socket
        _socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
        try
        {
            _socket.Bind(_localEndPoint);
            _configureSocket?.Invoke(_socket);
            await _logger.InfoAsync($"UdpListener started on {_localEndPoint}", cancellationToken);
        }
        catch (Exception ex)
        {
            await _logger.CriticalAsync(
                "Failed to initialize UdpListener socket",
                ex,
                cancellationToken
            );
            await DisposeAsync();
            throw;
        }

        EndPoint remoteEndPoint = new IPEndPoint(IPAddress.Any, 0);

        // 2. Enter the receive loop
        while (!cancellationToken.IsCancellationRequested)
        {
            var rentedBuffer = ArrayPool<byte>.Shared.Rent(_receiveBufferSize);
            SocketReceiveFromResult receiveResult;

            try
            {
                receiveResult = await _socket.ReceiveFromAsync(
                    rentedBuffer,
                    SocketFlags.None,
                    remoteEndPoint,
                    cancellationToken
                );
            }
            catch (OperationCanceledException)
            {
                ArrayPool<byte>.Shared.Return(rentedBuffer);
                break; // Normal cancellation
            }
            catch (Exception ex)
            {
                ArrayPool<byte>.Shared.Return(rentedBuffer);
                await _logger.ErrorAsync("UdpListener receive error", ex, cancellationToken);
                continue; // Decide whether to continue or break based on exception
            }

            // 3. Produce the message
            // Ownership of the rented buffer is transferred to the PooledUdpMessage,
            // which is then yielded to the consumer.
            yield return new PooledUdpMessage(
                rentedBuffer,
                receiveResult.ReceivedBytes,
                (IPEndPoint)receiveResult.RemoteEndPoint
            );
        }
    }

    /// <summary>
    /// Asynchronously disposes the underlying socket.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        if (_socket != null)
        {
            try
            {
                _socket.Close();
                _socket.Dispose();
            }
            catch (Exception ex)
            {
                await _logger.WarningAsync("Error disposing UdpListener socket", ex);
            }
            _socket = null;
        }
    }
}
