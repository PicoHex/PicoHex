namespace Pico.Node;

/// <summary>
/// High-performance UDP node
/// </summary>
public class UdpNode : IDisposable
{
    private readonly UdpSocket _udpSocket;
    private readonly IUdpHandler _handler;
    private readonly CancellationTokenSource _cts = new();
    private bool _disposed;

    /// <summary>
    /// Creates a high-performance UDP node
    /// </summary>
    public UdpNode(IUdpHandler handler, IPEndPoint localEndPoint)
    {
        _handler = handler ?? throw new ArgumentNullException(nameof(handler));
        _udpSocket = new UdpSocket();

        // Bind to specified endpoint or random port
        _udpSocket.Bind(localEndPoint);

        // Subscribe to data received event
        _udpSocket.DataReceived += OnDataReceived;
    }

    /// <summary>
    /// Local endpoint
    /// </summary>
    public IPEndPoint LocalEndPoint => _udpSocket.LocalEndPoint;

    /// <summary>
    /// Asynchronously sends data (using memory pool)
    /// </summary>
    public ValueTask SendAsync(ReadOnlyMemory<byte> data, IPEndPoint remoteEndPoint)
    {
        return _disposed
            ? throw new ObjectDisposedException(nameof(UdpNode))
            : _udpSocket.SendAsync(data, remoteEndPoint);
    }

    /// <summary>
    /// Asynchronously sends data (using memory pool)
    /// </summary>
    public ValueTask SendAsync(byte[] data, IPEndPoint remoteEndPoint)
    {
        return SendAsync(data.AsMemory(), remoteEndPoint);
    }

    /// <summary>
    /// Handles received data (zero-copy)
    /// </summary>
    private async void OnDataReceived(ReadOnlyMemory<byte> data, EndPoint remoteEndPoint)
    {
        if (_disposed || _cts.IsCancellationRequested)
            return;

        try
        {
            var remoteIpEndPoint = (IPEndPoint)remoteEndPoint;

            // Pass memory reference directly, avoiding copy
            await _handler.HandleAsync(data, remoteIpEndPoint, _cts.Token);

            // If handler implements IPooledUdpHandler, notify it that processing is complete
            if (_handler is IPooledUdpHandler pooledHandler)
            {
                pooledHandler.OnHandled();
            }
        }
        catch (Exception ex)
        {
            // Log error but don't interrupt processing
            Console.WriteLine($"Error processing UDP data: {ex.Message}");
        }
    }

    /// <summary>
    /// Releases resources
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _cts.Cancel();
        _udpSocket.Dispose();
        _cts.Dispose();
        GC.SuppressFinalize(this);
    }
}
