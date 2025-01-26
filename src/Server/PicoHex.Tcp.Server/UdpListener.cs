namespace PicoHex.Tcp.Server;

public class UdpListener : IDisposable, IAsyncDisposable
{
    private readonly Socket _socket;
    private readonly CancellationTokenSource _cts = new();
    private readonly MemoryPool<byte> _memoryPool = MemoryPool<byte>.Shared;
    private bool _disposed;

    public event Action<byte[], IPEndPoint>? DataReceived;
    public event Action<Exception>? ErrorOccurred;

    public UdpListener(IPEndPoint localEndPoint)
    {
        _socket = new Socket(localEndPoint.AddressFamily, SocketType.Dgram, ProtocolType.Udp);
        _socket.Bind(localEndPoint);
    }

    public void Start()
    {
        _ = ReceiveLoopAsync(_cts.Token);
    }

    private async Task ReceiveLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            var bufferOwner = _memoryPool.Rent(65507); // Max UDP packet size
            var buffer = bufferOwner.Memory;
            SocketReceiveFromResult result;

            try
            {
                result = await _socket.ReceiveFromAsync(
                    buffer,
                    SocketFlags.None,
                    new IPEndPoint(IPAddress.Any, 0),
                    ct
                );
            }
            catch (Exception ex) when (ex is OperationCanceledException or ObjectDisposedException)
            {
                bufferOwner.Dispose();
                break;
            }
            catch (Exception ex)
            {
                bufferOwner.Dispose();
                OnError(ex);
                continue;
            }

            // 立即复制数据并释放原始缓冲区
            var data = buffer.Slice(0, result.ReceivedBytes).ToArray();
            bufferOwner.Dispose();

            var remoteEP = (IPEndPoint)result.RemoteEndPoint;
            DataReceived?.Invoke(data, remoteEP);
        }
    }

    public async ValueTask SendAsync(
        byte[] data,
        IPEndPoint remoteEP,
        CancellationToken ct = default
    )
    {
        try
        {
            await _socket.SendToAsync(new ArraySegment<byte>(data), SocketFlags.None, remoteEP, ct);
        }
        catch (Exception ex)
        {
            OnError(ex);
            throw;
        }
    }

    private void OnError(Exception ex)
    {
        ErrorOccurred?.Invoke(ex);
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    public async ValueTask DisposeAsync()
    {
        await DisposeAsyncCore().ConfigureAwait(false);
        Dispose(false);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (_disposed)
            return;

        if (disposing)
        {
            _cts.Cancel();
            _socket.Dispose();
            _cts.Dispose();
        }

        _disposed = true;
    }

    protected virtual async ValueTask DisposeAsyncCore()
    {
        await _cts.CancelAsync();
        try
        {
            // 优雅关闭套接字
            await Task.Run(() => _socket.Shutdown(SocketShutdown.Both), _cts.Token);
        }
        catch
        {
            // 忽略关闭期间的异常
        }
        finally
        {
            _socket.Close();
        }
    }

    ~UdpListener()
    {
        Dispose(false);
    }
}
