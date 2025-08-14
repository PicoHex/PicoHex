namespace Pico.Node;

/// <summary>
/// Configuration options for TcpNode
/// </summary>
public sealed class TcpNodeOptions
{
    /// <summary>
    /// The IP address the server will listen on. Defaults to IPAddress.Any (0.0.0.0 for IPv4, or :: for IPv6 if dual-stack is enabled).
    /// </summary>
    public IPAddress IpAddress { get; set; } = IPAddress.Any;

    /// <summary>
    /// The port the server will listen on. Defaults to 8080 (V4's convenience).
    /// </summary>
    public int Port { get; set; } = 8080;

    /// <summary>
    /// The maximum number of concurrent connections the server can handle.
    /// This also determines the number of worker tasks. Defaults to Environment.ProcessorCount (V4's adaptive approach).
    /// </summary>
    public int MaxConcurrentConnections { get; set; } = Environment.ProcessorCount;

    /// <summary>
    /// The capacity of the internal channel for accepted connections.
    /// This acts as a buffer for backpressure. Defaults to 1000 (V4's larger buffer).
    /// </summary>
    public int ChannelCapacity { get; set; } = 1000;

    /// <summary>
    /// The maximum length of the pending connections queue.
    /// This is passed to Socket.Listen(). Defaults to 100.
    /// </summary>
    public int ListenBacklog { get; set; } = 100;

    /// <summary>
    /// Timeout for graceful shutdown of all tasks. Defaults to 5 seconds (V4's faster shutdown).
    /// </summary>
    public TimeSpan StopTimeout { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Optional exception handler for unhandled exceptions during connection processing.
    /// </summary>
    public Action<Exception, IPEndPoint?>? ExceptionHandler { get; set; }
}
