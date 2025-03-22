namespace PicoHex.Socket;

public sealed class TcpListener : IDisposable, IAsyncDisposable
{
    private readonly System.Net.Sockets.Socket _listenerSocket;
    private readonly CancellationTokenSource _cts = new();
    private readonly MemoryPool<byte> _memoryPool = MemoryPool<byte>.Shared;
    private readonly ConcurrentDictionary<Guid, TcpClientHandler> _clients = new();
    private bool _disposed;

    // 事件定义
    public event Action<Guid>? ClientConnected;
    public event Action<Guid>? ClientDisconnected;
    public event Action<Exception>? ErrorOccurred;

    public TcpListener(IPEndPoint localEndPoint, int backlog = 100)
    {
        _listenerSocket = new System.Net.Sockets.Socket(
            localEndPoint.AddressFamily,
            SocketType.Stream,
            ProtocolType.Tcp
        );
        _listenerSocket.Bind(localEndPoint);
        _listenerSocket.Listen(backlog);
        _listenerSocket.NoDelay = true;
        // TODO
        ClientConnected += clientId => Console.WriteLine($"Client connected: {clientId}");
        ClientDisconnected += clientId => Console.WriteLine($"Client disconnected: {clientId}");
        ErrorOccurred += ex => Console.WriteLine($"Error occurred: {ex}");
    }

    public void Start()
    {
        _ = AcceptLoopAsync(_cts.Token);
    }

    private async Task AcceptLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                var clientSocket = await _listenerSocket.AcceptAsync(ct);
                var clientHandler = new TcpClientHandler(clientSocket, _memoryPool);

                // 注册客户端事件
                clientHandler.Disconnected += OnClientDisconnected;
                clientHandler.ErrorOccurred += OnClientError;

                var clientId = Guid.NewGuid();
                if (_clients.TryAdd(clientId, clientHandler))
                {
                    // 触发客户端连接事件
                    ClientConnected?.Invoke(clientId);
                    _ = clientHandler.StartReceiveLoopAsync(ct);
                }
                else
                {
                    await clientHandler.DisposeAsync();
                    OnError(new Exception($"Failed to track client {clientId}"));
                }
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                // 触发错误事件
                OnError(new Exception("Accept error", ex));
                await Task.Delay(1000, ct); // 错误恢复延迟
            }
        }
    }

    private void OnClientDisconnected(Guid clientId)
    {
        if (!_clients.TryRemove(clientId, out var client))
            return;
        client.Dispose();
        // 触发客户端断开事件
        ClientDisconnected?.Invoke(clientId);
    }

    private void OnClientError(Exception ex)
    {
        // 传递客户端错误到主错误事件
        OnError(ex);
    }

    private void OnError(Exception ex)
    {
        // 安全触发错误事件
        ErrorOccurred?.Invoke(ex);
    }

    public async ValueTask SendAsync(
        Guid clientId,
        ReadOnlyMemory<byte> data,
        CancellationToken ct = default
    )
    {
        if (_clients.TryGetValue(clientId, out var client))
        {
            try
            {
                await client.SendAsync(data, ct);
            }
            catch (Exception ex)
            {
                OnError(new Exception($"Send failed to {clientId}", ex));
                throw;
            }
        }
        else
        {
            throw new KeyNotFoundException($"Client {clientId} not connected");
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _cts.Cancel();
        foreach (var client in _clients.Values)
        {
            client.Dispose();
        }
        _clients.Clear();
        _listenerSocket.Dispose();
        _cts.Dispose();
        _disposed = true;
        GC.SuppressFinalize(this);
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;

        await _cts.CancelAsync();
        await Task.WhenAll(_clients.Values.Select(client => client.DisposeAsync().AsTask()));
        _clients.Clear();
        _listenerSocket.Dispose();
        _cts.Dispose();
        _disposed = true;
        GC.SuppressFinalize(this);
    }

    private sealed class TcpClientHandler : IDisposable, IAsyncDisposable
    {
        private readonly System.Net.Sockets.Socket _socket;
        private readonly MemoryPool<byte> _memoryPool;
        private bool _disposed;
        private readonly SemaphoreSlim _sendLock = new(1, 1);

        public event Action<Guid>? Disconnected;
        public event Action<Exception>? ErrorOccurred;

        public Guid ClientId { get; } = Guid.NewGuid();

        public TcpClientHandler(System.Net.Sockets.Socket socket, MemoryPool<byte> memoryPool)
        {
            _socket = socket;
            _memoryPool = memoryPool;
            _socket.NoDelay = true;
        }

        public async Task StartReceiveLoopAsync(CancellationToken ct)
        {
            try
            {
                while (!ct.IsCancellationRequested && !_disposed)
                {
                    using var bufferOwner = _memoryPool.Rent(4096);
                    var buffer = bufferOwner.Memory;

                    int bytesRead;
                    try
                    {
                        bytesRead = await _socket.ReceiveAsync(buffer, SocketFlags.None, ct);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                    catch (Exception ex)
                    {
                        HandleError(ex);
                        break;
                    }

                    if (bytesRead != 0)
                        continue; // 正常关闭
                    Disconnected?.Invoke(ClientId);
                    break;
                }
            }
            catch (Exception ex)
            {
                HandleError(ex);
            }
            finally
            {
                Disconnected?.Invoke(ClientId);
                await DisposeAsync();
            }
        }

        public async ValueTask SendAsync(ReadOnlyMemory<byte> data, CancellationToken ct)
        {
            await _sendLock.WaitAsync(ct);
            try
            {
                var totalSent = 0;
                while (totalSent < data.Length && !ct.IsCancellationRequested)
                {
                    var sent = await _socket.SendAsync(data[totalSent..], SocketFlags.None, ct);
                    if (sent == 0)
                        throw new SocketException((int)SocketError.ConnectionReset);
                    totalSent += sent;
                }
            }
            catch (Exception ex)
            {
                HandleError(ex);
                throw;
            }
            finally
            {
                _sendLock.Release();
            }
        }

        private void HandleError(Exception ex)
        {
            if (_disposed)
                return;

            ErrorOccurred?.Invoke(ex);
            Disconnected?.Invoke(ClientId);
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            try
            {
                _socket.Shutdown(SocketShutdown.Both);
            }
            catch
            {
                // 忽略关闭异常
            }
            finally
            {
                _socket.Dispose();
                _sendLock.Dispose();
                _disposed = true;
            }
        }

        public async ValueTask DisposeAsync()
        {
            if (_disposed)
                return;

            try
            {
                await Task.Run(() => _socket.Shutdown(SocketShutdown.Both));
            }
            catch
            {
                // 忽略关闭异常
            }
            finally
            {
                _socket.Dispose();
                _sendLock.Dispose();
                _disposed = true;
            }
        }
    }
}

public static class SocketExtensions
{
    public static async Task<System.Net.Sockets.Socket> AcceptAsync(
        this System.Net.Sockets.Socket socket,
        CancellationToken ct
    )
    {
        var tcs = new TaskCompletionSource<System.Net.Sockets.Socket>(
            TaskCreationOptions.RunContinuationsAsynchronously
        );
        await using var reg = ct.Register(() => tcs.TrySetCanceled());

        try
        {
            socket.BeginAccept(
                ar =>
                {
                    try
                    {
                        var accepted = socket.EndAccept(ar);
                        tcs.TrySetResult(accepted);
                    }
                    catch (Exception ex)
                    {
                        tcs.TrySetException(ex);
                    }
                },
                null
            );

            return await tcs.Task;
        }
        catch (OperationCanceledException)
        {
            throw new OperationCanceledException("Accept operation cancelled", ct);
        }
    }
}
