namespace PicoHex.Transport;

public interface ITransportProtocol
{
    // 连接控制
    Task ConnectAsync(Uri endpoint, CancellationToken ct = default);
    Task DisconnectAsync(CancellationToken ct = default);

    // 数据传输
    Task SendAsync(byte[] payload, CancellationToken ct = default);
    IObservable<byte[]> DataReceived { get; }

    // 状态管理
    TransportType ProtocolType { get; }
    ConnectionState ConnectionStatus { get; }
    event Action<ConnectionState> ConnectionStateChanged;

    // 配置
    TimeSpan DefaultTimeout { get; set; }
    void ConfigureRetryPolicy(RetryPolicy policy);
}

public record RetryPolicy(int MaxAttempts, TimeSpan Delay);
