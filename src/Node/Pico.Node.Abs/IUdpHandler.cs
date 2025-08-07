namespace Pico.Node.Abs;

/// <summary>
/// 扩展的UDP处理器接口（零拷贝优化版）
/// </summary>
public interface IUdpHandler
{
    /// <summary>
    /// 处理接收到的UDP数据报（零拷贝）
    /// </summary>
    /// <param name="data">接收到的数据（内存切片）</param>
    /// <param name="remoteEndPoint">远程端点</param>
    /// <param name="sendResponse">响应发送函数</param>
    /// <param name="cancellationToken">取消令牌</param>
    ValueTask HandleAsync(
        ReadOnlyMemory<byte> data,
        EndPoint remoteEndPoint,
        Func<ReadOnlyMemory<byte>, ValueTask> sendResponse,
        CancellationToken cancellationToken = default
    );
}
