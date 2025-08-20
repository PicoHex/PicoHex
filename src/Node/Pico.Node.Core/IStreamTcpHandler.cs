namespace Pico.Node.Core;

/// <summary>
/// Stream-based TCP handler interface (compatibility mode)
/// </summary>
public interface IStreamTcpHandler
{
    /// <summary>
    /// Handles TCP connection (based on Stream)
    /// </summary>
    /// <param name="stream">Network stream</param>
    /// <param name="remoteEndPoint">Remote endpoint</param>
    /// <param name="cancellationToken">Cancellation token</param>
    ValueTask HandleAsync(
        Stream stream,
        IPEndPoint remoteEndPoint,
        CancellationToken cancellationToken = default
    );
}
