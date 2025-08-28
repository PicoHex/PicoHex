namespace Pico.Node;

public sealed class TcpNodeOptions
{
    /// <summary>
    /// The endpoint to listen on (required)
    /// </summary>
    public required IPEndPoint Endpoint { get; init; }

    /// <summary>
    /// The message handler (required)
    /// </summary>
    public required Func<ITcpHandler> HandlerFactory { get; init; }

    /// <summary>
    /// Logger instance (optional)
    /// </summary>
    public required ILogger<TcpNode> Logger { get; init; }

    /// <summary>
    /// Maximum concurrent connections (default: 1000)
    /// </summary>
    public int MaxConnections { get; init; } = 1000;

    /// <summary>
    /// Disable Nagle's algorithm for lower latency (default: true)
    /// </summary>
    public bool NoDelay { get; init; } = true;

    /// <summary>
    /// Linger option for socket (default: Don't linger on close)
    /// </summary>
    public LingerOption LingerState { get; init; } = new(false, 0);

    /// <summary>
    /// Connection backlog size (default: 100)
    /// </summary>
    public int BacklogSize { get; init; } = 100;
}
