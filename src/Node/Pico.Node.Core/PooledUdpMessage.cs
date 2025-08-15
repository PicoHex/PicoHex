namespace Pico.Node.Core;

public readonly struct PooledUdpMessage(
    byte[] rentedBuffer,
    int dataLength,
    IPEndPoint remoteEndPoint
) : IDisposable
{
    /// <summary>
    /// 包含接收到的数据的内存段。
    /// </summary>
    public ReadOnlyMemory<byte> Data { get; } = new(rentedBuffer, 0, dataLength);

    /// <summary>
    /// 发送数据的远程终结点。
    /// </summary>
    public IPEndPoint RemoteEndPoint { get; } = remoteEndPoint;

    /// <summary>
    /// 将内部缓冲区归还给内存池。
    /// </summary>
    public void Dispose()
    {
        if (rentedBuffer != null)
        {
            ArrayPool<byte>.Shared.Return(rentedBuffer);
        }
    }
}
