namespace PicoHex.Transport;

public class TcpServerTransport(ILogger<TcpServerTransport> logger) : ITcpTransport
{
    private readonly List<TcpClient> _activeClients = new();
    private readonly Channel<IConnection> _connectionChannel =
        Channel.CreateUnbounded<IConnection>();
    private TcpListener? _listener;
    private CancellationTokenSource? _listenCts;

    public event Action<TransportError>? OnError;
    public event Action<IConnection>? OnNewConnection;

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        await logger.InfoAsync(
            "TCP server transport starting...",
            cancellationToken: cancellationToken
        );
        _listenCts = new CancellationTokenSource();
        _ = Task.Run(() => ListenLoop(_listenCts.Token), _listenCts.Token);
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        await logger.InfoAsync(
            "TCP server transport stopping...",
            cancellationToken: cancellationToken
        );
        await _listenCts?.CancelAsync()!;

        // 关闭所有活跃连接
        foreach (var client in _activeClients.ToArray())
        {
            SafeDispose(client);
        }

        _listener?.Stop();
        _connectionChannel.Writer.Complete();
    }

    public async Task BindAsync(IPEndPoint endpoint, CancellationToken cancellationToken = default)
    {
        _listener = new TcpListener(endpoint);
        try
        {
            _listener.Start();
            await logger.InfoAsync(
                $"TCP server bound to {endpoint}",
                cancellationToken: cancellationToken
            );
        }
        catch (SocketException ex)
        {
            OnError?.Invoke(
                new TransportError(
                    TransportErrorType.ConnectionFailed,
                    $"Bind failed: {ex.SocketErrorCode}"
                )
            );
            throw;
        }
    }

    private async Task ListenLoop(CancellationToken listenToken)
    {
        if (_listener == null)
            return;

        try
        {
            while (!listenToken.IsCancellationRequested)
            {
                var tcpClient = await _listener.AcceptTcpClientAsync(listenToken);
                await logger.DebugAsync(
                    $"New connection from {tcpClient.Client.RemoteEndPoint}",
                    cancellationToken: listenToken
                );

                var connection = new TcpConnection(tcpClient, logger);
                _activeClients.Add(tcpClient);

                // 触发事件并缓存连接
                OnNewConnection?.Invoke(connection);
                await _connectionChannel.Writer.WriteAsync(connection, listenToken);
            }
        }
        catch (OperationCanceledException)
        {
            // 正常停止
        }
        catch (Exception ex)
        {
            OnError?.Invoke(
                new TransportError(
                    TransportErrorType.ConnectionFailed,
                    $"Listen loop failed: {ex.Message}"
                )
            );
        }
    }

    private void SafeDispose(TcpClient client)
    {
        try
        {
            client.Dispose();
            _activeClients.Remove(client);
        }
        catch (Exception ex)
        {
            logger.Error("Error disposing client", ex);
        }
    }

    // TCP 连接实现
    private class TcpConnection(TcpClient client, ILogger logger) : IConnection
    {
        private readonly TcpClient _client =
            client ?? throw new ArgumentNullException(nameof(client));
        private readonly NetworkStream _stream = client.GetStream();
        private readonly CancellationTokenSource _cts = new();

        public IPEndPoint LocalEndPoint { get; } =
            (IPEndPoint)(
                client.Client.LocalEndPoint
                ?? throw new InvalidOperationException("Local endpoint not available")
            );

        public IPEndPoint RemoteEndPoint { get; } =
            (IPEndPoint)(
                client.Client.RemoteEndPoint
                ?? throw new InvalidOperationException("Remote endpoint not available")
            );

        public async Task SendAsync(
            ReadOnlyMemory<byte> data,
            CancellationToken cancellationToken = default
        )
        {
            try
            {
                await _stream.WriteAsync(data, cancellationToken);
                await logger.TraceAsync(
                    $"Sent {data.Length} bytes to {RemoteEndPoint}",
                    cancellationToken: cancellationToken
                );
            }
            catch (Exception ex)
            {
                await logger.ErrorAsync(
                    "Send failed to {RemoteEndPoint}",
                    ex,
                    cancellationToken: cancellationToken
                );
                throw new TransportException("Send failed", ex);
            }
        }

        public async IAsyncEnumerable<byte[]> ReceiveAsync(
            [EnumeratorCancellation] CancellationToken cancellationToken = default
        )
        {
            var combinedToken = CancellationTokenSource
                .CreateLinkedTokenSource(cancellationToken, _cts.Token)
                .Token;

            var buffer = new byte[4096];
            while (!combinedToken.IsCancellationRequested && _client.Connected)
            {
                int bytesRead;
                try
                {
                    bytesRead = await _stream.ReadAsync(buffer, combinedToken);
                    if (bytesRead == 0)
                        break; // 连接关闭
                }
                catch (Exception ex)
                {
                    await logger.ErrorAsync(
                        $"Receive failed from {RemoteEndPoint}",
                        ex,
                        cancellationToken: combinedToken
                    );
                    throw new TransportException("Receive failed", ex);
                }

                var data = new byte[bytesRead];
                Array.Copy(buffer, data, bytesRead);
                await logger.TraceAsync(
                    $"Received {bytesRead} bytes from {RemoteEndPoint}",
                    cancellationToken: combinedToken
                );
                yield return data;
            }
        }

        public void Dispose()
        {
            try
            {
                _cts.Cancel();
                _stream.Dispose();
                _client.Dispose();
                logger.Info($"Connection closed: {RemoteEndPoint}");
            }
            catch (Exception ex)
            {
                logger.Error("Error disposing connection", ex);
            }
        }
    }

    public void Dispose()
    {
        throw new NotImplementedException();
    }
}
