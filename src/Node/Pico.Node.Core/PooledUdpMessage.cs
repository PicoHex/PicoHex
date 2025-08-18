namespace Pico.Node.Core;

/// <summary>
/// Represents a UDP message with a buffer rented from the shared ArrayPool.
/// This struct MUST be disposed to return the buffer to the pool.
/// </summary>
public readonly struct PooledUdpMessage(
    byte[] rentedBuffer,
    int dataLength,
    IPEndPoint remoteEndPoint
) : IDisposable
{
    /// <summary>
    /// A memory segment representing the received datagram.
    /// </summary>
    public ReadOnlyMemory<byte> Data { get; } = new(rentedBuffer, 0, dataLength);

    /// <summary>
    /// The remote endpoint from which the datagram was received.
    /// </summary>
    public IPEndPoint RemoteEndPoint { get; } = remoteEndPoint;

    /// <summary>
    /// Returns the internal buffer to the ArrayPool.
    /// </summary>
    public void Dispose()
    {
        ArrayPool<byte>.Shared.Return(rentedBuffer);
    }
}
