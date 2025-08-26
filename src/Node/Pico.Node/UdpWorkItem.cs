namespace Pico.Node;

/// <summary>
/// Represents a work item containing a received UDP datagram
/// Implements IDisposable to properly return buffers to the ArrayPool
/// </summary>
internal sealed class UdpWorkItem : IDisposable
{
    private bool _disposed;

    /// <summary>
    /// Gets the buffer containing the datagram data
    /// </summary>
    public byte[] Buffer { get; }

    /// <summary>
    /// Gets the number of bytes in the datagram
    /// </summary>
    public int Count { get; }

    /// <summary>
    /// Gets the remote endpoint that sent the datagram
    /// </summary>
    public IPEndPoint RemoteEndPoint { get; }

    /// <summary>
    /// Initializes a new instance of the UdpWorkItem class
    /// </summary>
    /// <param name="buffer">The buffer containing the datagram</param>
    /// <param name="count">The number of bytes in the datagram</param>
    /// <param name="remoteEndPoint">The remote endpoint</param>
    public UdpWorkItem(byte[] buffer, int count, IPEndPoint remoteEndPoint)
    {
        Buffer = buffer;
        Count = count;
        RemoteEndPoint = remoteEndPoint;
    }

    /// <summary>
    /// Gets the datagram data as a ReadOnlyMemory
    /// </summary>
    public ReadOnlyMemory<byte> Datagram => new ReadOnlyMemory<byte>(Buffer, 0, Count);

    /// <summary>
    /// Disposes the work item and returns the buffer to the ArrayPool
    /// </summary>
    public void Dispose()
    {
        if (!_disposed)
        {
            ArrayPool<byte>.Shared.Return(Buffer);
            _disposed = true;
        }
    }
}
