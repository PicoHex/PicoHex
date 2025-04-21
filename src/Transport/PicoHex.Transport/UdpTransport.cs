namespace PicoHex.Transport;

public class UdpTransport(ILogger<UdpTransport> logger) : IUdpTransport
{
    private UdpClient? _udpClient;
    private CancellationTokenSource? _receiveCts;
    private IPEndPoint? _bindEndpoint;
    private readonly Channel<(IPEndPoint From, byte[] Data)> _receiveChannel =
        Channel.CreateUnbounded<(IPEndPoint, byte[])>();

    public event Action<TransportError>? OnError;

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (_udpClient != null)
            return;

        try
        {
            _udpClient = _bindEndpoint != null ? new UdpClient(_bindEndpoint) : new UdpClient(0); // 随机端口

            await logger.InfoAsync(
                $"UDP transport started on {LocalEndPoint}",
                cancellationToken: cancellationToken
            );
            _receiveCts = new CancellationTokenSource();
            _ = Task.Run(() => ReceiveLoop(_receiveCts.Token), _receiveCts.Token);
        }
        catch (SocketException ex)
        {
            HandleError(
                TransportErrorType.ConnectionFailed,
                $"Failed to start UDP: {ex.SocketErrorCode}",
                ex
            );
            throw new TransportException("UDP startup failed", ex);
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        await logger.InfoAsync("Stopping UDP transport...", cancellationToken: cancellationToken);
        await _receiveCts?.CancelAsync()!;
        _udpClient?.Close();
        _udpClient = null;
        _receiveChannel.Writer.Complete();
    }

    public async Task BindAsync(IPEndPoint endpoint, CancellationToken cancellationToken = default)
    {
        _bindEndpoint = endpoint;
        if (_udpClient != null)
        {
            _udpClient.Client.Bind(endpoint);
            await logger.InfoAsync($"Bound to {endpoint}", cancellationToken: cancellationToken);
        }
    }

    public IPEndPoint LocalEndPoint =>
        _udpClient?.Client.LocalEndPoint as IPEndPoint
        ?? throw new InvalidOperationException("Not initialized");

    public async Task SendAsync(
        IPEndPoint target,
        ReadOnlyMemory<byte> data,
        CancellationToken cancellationToken = default
    )
    {
        if (_udpClient == null)
            throw new InvalidOperationException("Transport not started");

        try
        {
            var sentBytes = await _udpClient.SendAsync(data.ToArray(), data.Length, target);
            await logger.TraceAsync(
                $"Sent {sentBytes} bytes to {target}",
                cancellationToken: cancellationToken
            );
        }
        catch (SocketException ex)
        {
            HandleError(
                TransportErrorType.ConnectionFailed,
                $"Send to {target} failed: {ex.SocketErrorCode}",
                ex
            );
            throw new TransportException("UDP send failed", ex);
        }
    }

    public async IAsyncEnumerable<(IPEndPoint From, byte[] Data)> ReceiveAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken = default
    )
    {
        await foreach (var item in _receiveChannel.Reader.ReadAllAsync(cancellationToken))
        {
            yield return item;
        }
    }

    private async Task ReceiveLoop(CancellationToken cancellationToken)
    {
        if (_udpClient == null)
            return;

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var result = await _udpClient.ReceiveAsync(cancellationToken);
                await logger.TraceAsync(
                    $"Received {result.Buffer.Length} bytes from {result.RemoteEndPoint}",
                    cancellationToken: cancellationToken
                );

                await _receiveChannel.Writer.WriteAsync(
                    (result.RemoteEndPoint, result.Buffer),
                    cancellationToken
                );
            }
        }
        catch (OperationCanceledException)
        {
            // 正常停止
        }
        catch (Exception ex)
        {
            HandleError(TransportErrorType.DataCorruption, "Receive loop failed", ex);
        }
    }

    private void HandleError(TransportErrorType type, string message, Exception? ex = null)
    {
        var error = new TransportError(type, message, ex);
        logger.Error(message, ex);
        OnError?.Invoke(error);
    }

    public void Dispose()
    {
        _udpClient?.Dispose();
        _receiveCts?.Dispose();
        GC.SuppressFinalize(this);
    }
}
