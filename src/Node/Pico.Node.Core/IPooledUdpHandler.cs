namespace Pico.Node.Core;

/// <summary>
/// Memory pool-aware UDP handler interface
/// </summary>
public interface IPooledUdpHandler : IUdpHandler
{
    /// <summary>
    /// Called after processing to indicate the handler has completed processing the data
    /// Implementations can return buffers to the memory pool here
    /// </summary>
    void OnHandled();
}
