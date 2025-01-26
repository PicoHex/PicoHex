namespace PicoHex.Socket;

public sealed class UdpClient : IDisposable, IAsyncDisposable
{
    private readonly System.Net.Sockets.Socket _socket;
    private readonly MemoryPool<byte> _memoryPool = MemoryPool<byte>.Shared;
    private readonly CancellationTokenSource _cts = new();
    private bool _disposed;

    // 事件定义
    public event Action<byte[], IPEndPoint>? DataReceived;
    public event Action<Exception>? ErrorOccurred;

    public UdpClient(AddressFamily addressFamily = AddressFamily.InterNetwork)
    {
        _socket = new System.Net.Sockets.Socket(addressFamily, SocketType.Dgram, ProtocolType.Udp);
    }

    public UdpClient(IPEndPoint localEndPoint)
    {
        _socket = new System.Net.Sockets.Socket(
            localEndPoint.AddressFamily,
            SocketType.Dgram,
            ProtocolType.Udp
        );
        _socket.Bind(localEndPoint);
        _ = StartReceiveLoopAsync(_cts.Token);
    }

    private async Task StartReceiveLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            IMemoryOwner<byte> bufferOwner = _memoryPool.Rent(65507); // UDP最大包大小
            Memory<byte> buffer = bufferOwner.Memory;

            try
            {
                var result = await _socket.ReceiveFromAsync(
                    buffer,
                    SocketFlags.None,
                    new IPEndPoint(IPAddress.Any, 0),
                    ct
                );

                var data = buffer.Slice(0, result.ReceivedBytes).ToArray();
                var remoteEndPoint = (IPEndPoint)result.RemoteEndPoint;
                DataReceived?.Invoke(data, remoteEndPoint);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                ErrorOccurred?.Invoke(ex);
            }
            finally
            {
                bufferOwner.Dispose();
            }
        }
    }

    public async Task SendAsync(
        byte[] data,
        IPEndPoint remoteEndPoint,
        CancellationToken ct = default
    )
    {
        try
        {
            await _socket.SendToAsync(
                new ArraySegment<byte>(data),
                SocketFlags.None,
                remoteEndPoint,
                ct
            );
        }
        catch (Exception ex)
        {
            ErrorOccurred?.Invoke(ex);
            throw;
        }
    }

    public async Task<byte[]> RequestAsync(
        byte[] data,
        IPEndPoint remoteEndPoint,
        TimeSpan timeout,
        CancellationToken ct = default
    )
    {
        using var timeoutCts = new CancellationTokenSource(timeout);
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, timeoutCts.Token);

        var tcs = new TaskCompletionSource<byte[]>();
        Action<byte[], IPEndPoint> handler = (responseData, ep) =>
        {
            if (ep.Equals(remoteEndPoint))
            {
                tcs.TrySetResult(responseData);
            }
        };

        DataReceived += handler;
        try
        {
            await SendAsync(data, remoteEndPoint, linkedCts.Token);
            return await tcs.Task;
        }
        finally
        {
            DataReceived -= handler;
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _cts.Cancel();
        _socket.Dispose();
        _cts.Dispose();
        _disposed = true;
        GC.SuppressFinalize(this);
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;

        await _cts.CancelAsync();
        await Task.Run(() => _socket.Close());
        _socket.Dispose();
        _cts.Dispose();
        _disposed = true;
        GC.SuppressFinalize(this);
    }
}
