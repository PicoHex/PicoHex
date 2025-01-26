namespace PicoHex.Socket;

public sealed class TcpClient : IDisposable, IAsyncDisposable
{
    private readonly System.Net.Sockets.Socket _socket;
    private readonly MemoryPool<byte> _memoryPool = MemoryPool<byte>.Shared;
    private readonly CancellationTokenSource _cts = new();
    private bool _disposed;

    // 事件定义
    public event Action? Connected;
    public event Action<ReadOnlyMemory<byte>>? DataReceived;
    public event Action<Exception>? ErrorOccurred;
    public event Action? Disconnected;

    public TcpClient(AddressFamily addressFamily = AddressFamily.InterNetwork)
    {
        _socket = new System.Net.Sockets.Socket(addressFamily, SocketType.Stream, ProtocolType.Tcp);
        _socket.NoDelay = true;
    }

    public async Task ConnectAsync(IPEndPoint remoteEndPoint, CancellationToken ct = default)
    {
        try
        {
            await _socket.ConnectAsync(remoteEndPoint, ct);
            Connected?.Invoke();
            _ = ReceiveLoopAsync(_cts.Token);
        }
        catch (Exception ex)
        {
            ErrorOccurred?.Invoke(ex);
            throw;
        }
    }

    private async Task ReceiveLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested && _socket.Connected)
        {
            var bufferOwner = _memoryPool.Rent(4096);
            var buffer = bufferOwner.Memory;

            try
            {
                var bytesRead = await _socket.ReceiveAsync(buffer, SocketFlags.None, ct);
                if (bytesRead == 0)
                    break;

                var dataCopy = buffer.Slice(0, bytesRead).ToArray();
                DataReceived?.Invoke(dataCopy);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                ErrorOccurred?.Invoke(ex);
                break;
            }
            finally
            {
                bufferOwner.Dispose();
            }
        }

        Disconnected?.Invoke();
    }

    public async Task SendAsync(ReadOnlyMemory<byte> data, CancellationToken ct = default)
    {
        try
        {
            var totalSent = 0;
            while (totalSent < data.Length)
            {
                var sent = await _socket.SendAsync(data.Slice(totalSent), SocketFlags.None, ct);
                if (sent == 0)
                    throw new SocketException((int)SocketError.ConnectionReset);
                totalSent += sent;
            }
        }
        catch (Exception ex)
        {
            ErrorOccurred?.Invoke(ex);
            throw;
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _cts.Cancel();
        try
        {
            _socket.Shutdown(SocketShutdown.Both);
        }
        finally
        {
            _socket.Dispose();
            _cts.Dispose();
            _disposed = true;
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;

        await _cts.CancelAsync();
        try
        {
            await Task.Run(() => _socket.Shutdown(SocketShutdown.Both));
        }
        finally
        {
            _socket.Dispose();
            _cts.Dispose();
            _disposed = true;
        }
    }
}
