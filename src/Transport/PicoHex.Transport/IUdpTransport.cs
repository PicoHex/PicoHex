namespace PicoHex.Transport;

/// <summary>
/// 无连接传输层（如 UDP）
/// </summary>
public interface IUdpTransport : ITransport
{
    Task SendAsync(
        IPEndPoint target,
        ReadOnlyMemory<byte> data,
        CancellationToken cancellationToken = default
    );
    IAsyncEnumerable<(IPEndPoint From, byte[] Data)> ReceiveAsync(
        CancellationToken cancellationToken = default
    );
}
