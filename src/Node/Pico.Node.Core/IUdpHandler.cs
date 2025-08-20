namespace Pico.Node.Core;

/// <summary>
/// High-performance UDP handler interface
/// </summary>
public interface IUdpHandler
{
    /// <summary>
    /// Handles received UDP data (zero-copy version)
    /// </summary>
    /// <param name="data">Received data (memory segment)</param>
    /// <param name="remoteEndPoint">Remote endpoint</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <remarks>
    /// Implementations should not hold references to data for extended periods as the underlying buffer may be reused
    /// </remarks>
    ValueTask HandleAsync(
        ReadOnlyMemory<byte> data,
        IPEndPoint remoteEndPoint,
        CancellationToken cancellationToken = default
    );
}
