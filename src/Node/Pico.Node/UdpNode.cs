namespace Pico.Node;

/// <summary>
/// High-performance UDP node
/// </summary>
public class UdpNode : INode
{
    private readonly UdpSocket _udpSocket;
    private readonly IUdpHandler _handler;
    private readonly IPEndPoint _localEndPoint;
    private readonly CancellationTokenSource _cts = new();
    private bool _disposed;
    private bool _started;

    /// <summary>
    /// Creates a high-performance UDP node
    /// </summary>
    public UdpNode(IUdpHandler handler, IPEndPoint localEndPoint)
    {
        _handler = handler ?? throw new ArgumentNullException(nameof(handler));
        _localEndPoint = localEndPoint;
        _udpSocket = new UdpSocket();
    }

    /// <summary>
    /// Asynchronously starts the node
    /// </summary>
    public ValueTask StartAsync(CancellationToken cancellationToken = default)
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(UdpNode));
        if (_started)
            throw new InvalidOperationException("Already started");

        _udpSocket.Bind(_localEndPoint);
        _udpSocket.DataReceived += OnDataReceived;
        _started = true;
        return ValueTask.CompletedTask;
    }

    /// <summary>
    /// Asynchronously stops the node
    /// </summary>
    public ValueTask StopAsync(CancellationToken cancellationToken = default)
    {
        if (_disposed || !_started)
            return ValueTask.CompletedTask;

        _started = false;
        _cts.Cancel();
        _udpSocket.DataReceived -= OnDataReceived;
        _udpSocket.Dispose();
        return ValueTask.CompletedTask;
    }

    /// <summary>
    /// Handles received data (zero-copy)
    /// </summary>
    private async void OnDataReceived(ReadOnlyMemory<byte> data, EndPoint remoteEndPoint)
    {
        if (_disposed || !_started || _cts.IsCancellationRequested)
            return;

        try
        {
            var remoteIpEndPoint = (IPEndPoint)remoteEndPoint;

            // Pass memory reference directly, avoiding copy
            await _handler.HandleAsync(data, remoteIpEndPoint, _cts.Token);

            // If handler implements IPooledUdpHandler, notify it that processing is complete
            if (_handler is IUdpHandler pooledHandler)
            {
                pooledHandler.HandleAsync()
            }
        }
        catch (Exception ex)
        {
            // Log error but don't interrupt processing
            Console.WriteLine($"Error processing UDP data: {ex.Message}");
        }
    }

    /// <summary>
    /// Releases resources asynchronously
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;

        _disposed = true;
        await StopAsync();
        _cts.Dispose();
        GC.SuppressFinalize(this);
    }
}
