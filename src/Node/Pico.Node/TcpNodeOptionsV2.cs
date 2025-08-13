namespace Pico.Node;

/// <summary>
/// Provides configuration options for the TcpNodeV2 server.
/// </summary>
public sealed class TcpNodeOptionsV2
{
    /// <summary>
    /// The IP address the server will listen on. Required.
    /// </summary>
    public IPAddress IpAddress { get; set; } = IPAddress.Any;

    /// <summary>
    /// The port the server will listen on.
    /// </summary>
    public int Port { get; set; }

    /// <summary>
    /// The maximum number of connections that can be processed concurrently.
    /// This directly translates to the number of "worker" tasks.
    /// </summary>
    public int MaxConcurrentConnections { get; set; } = 100;

    /// <summary>
    /// The capacity of the internal channel that buffers accepted connections before they are processed.
    /// It's recommended to set this to a value slightly higher than MaxConcurrentConnections.
    /// </summary>
    public int ChannelCapacity { get; set; } = 120;

    /// <summary>
    /// An optional handler for exceptions that occur during connection processing.
    /// The IPEndPoint might be null if the exception occurs before the endpoint is resolved.
    /// </summary>
    public Action<Exception, IPEndPoint?>? ExceptionHandler { get; set; }

    /// <summary>
    /// The timeout for waiting for all tasks to shut down gracefully when StopAsync is called.
    /// </summary>
    public TimeSpan StopTimeout { get; set; } = TimeSpan.FromSeconds(15);
}
