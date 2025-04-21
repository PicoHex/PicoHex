namespace PicoHex.Transport;

/// <summary>
/// 面向连接的传输服务端（如 TCP Server）
/// </summary>
public interface ITcpTransport : ITransport
{
    event Action<IConnection> OnNewConnection;
    Task BindAsync(IPEndPoint endpoint, CancellationToken cancellationToken = default);
}

/// <summary>
/// 面向连接的传输客户端（如 TCP Client）
/// </summary>
public interface ITcpClientTransport : ITransport
{
    Task<IConnection> ConnectAsync(
        IPEndPoint endpoint,
        CancellationToken cancellationToken = default
    );
}

/// <summary>
/// 已建立的连接抽象
/// </summary>
public interface IConnection : IDisposable
{
    IPEndPoint LocalEndPoint { get; }
    IPEndPoint RemoteEndPoint { get; }
    Task SendAsync(ReadOnlyMemory<byte> data, CancellationToken cancellationToken = default);
    IAsyncEnumerable<byte[]> ReceiveAsync(CancellationToken cancellationToken = default);
}
