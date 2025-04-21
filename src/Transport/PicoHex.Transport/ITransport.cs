namespace PicoHex.Transport;

public interface ITransport : IDisposable
{
    Task StartAsync(CancellationToken cancellationToken = default);
    Task StopAsync(CancellationToken cancellationToken = default);
    event Action<TransportError> OnError;
}

// 传输层错误类型枚举
public enum TransportErrorType
{
    ConnectionFailed, // 连接失败
    Timeout, // 操作超时
    DataCorruption, // 数据损坏
    ProtocolViolation, // 协议违规
    ResourceExhausted // 资源耗尽（如内存不足）
}

// 传输层错误信息记录
public record TransportError(
    TransportErrorType ErrorType,
    string Message,
    Exception? InnerException = null
);
