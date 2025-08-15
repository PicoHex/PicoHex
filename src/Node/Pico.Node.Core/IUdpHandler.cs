// Updated handler interface
namespace Pico.Node.Core;

public interface IUdpHandler
{
    /// <summary>
    /// 异步处理一个 UDP 消息。
    /// </summary>
    /// <param name="message">包含数据和来源的池化消息对象。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <remarks>
    /// 实现者不需要手动 Dispose message 对象，UdpNode 会在使用后自动处理。
    /// </remarks>
    ValueTask HandleAsync(PooledUdpMessage message, CancellationToken cancellationToken);
}
