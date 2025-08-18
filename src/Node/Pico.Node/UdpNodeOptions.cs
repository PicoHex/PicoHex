namespace Pico.Node;

/// <summary>
/// Provides configuration options for the UdpNode.
/// </summary>
public class UdpNodeOptions
{
    /// <summary>
    /// The IP address to bind the UDP listener to. Defaults to IPAddress.Any.
    /// </summary>
    public IPAddress IpAddress { get; init; } = IPAddress.Any;

    /// <summary>
    /// The port to listen on. Defaults to 1337.
    /// </summary>
    public int Port { get; init; } = 1337;

    /// <summary>
    /// The maximum number of datagrams to process concurrently.
    /// Defaults to a value based on the processor count.
    /// </summary>
    public int MaxConcurrency { get; set; } = Math.Max(4, Environment.ProcessorCount * 2);

    /// <summary>
    /// The maximum number of unprocessed datagrams to queue.
    /// If the queue is full, older datagrams are dropped. Defaults to 1024.
    /// </summary>
    public int MaxQueueSize { get; set; } = 1024;

    /// <summary>
    /// The timeout for waiting for active processing tasks to complete during a graceful shutdown.
    /// Defaults to 5 seconds.
    /// </summary>
    public TimeSpan StopTasksTimeout { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>
    /// The timeout for waiting for server-related tasks (receiver, processor) during disposal.
    /// Defaults to 5 seconds.
    /// </summary>
    public TimeSpan DisposeServerTaskTimeout { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>
    /// The timeout for waiting for any remaining processing tasks during disposal.
    /// Defaults to 10 seconds.
    /// </summary>
    public TimeSpan DisposeProcessingTasksTimeout { get; set; } = TimeSpan.FromSeconds(10);

    /// <summary>
    /// An optional action to perform advanced configuration on the underlying Socket after it is bound.
    /// </summary>
    public Action<Socket>? ConfigureSocket { get; set; }

    /// <summary>
    /// The size of the buffer used for receiving data. Defaults to 65536 bytes.
    /// </summary>
    public int ReceiveBufferSize { get; set; } = 65536;

    /// <summary>
    /// An optional global exception handler for errors that occur during datagram processing.
    /// </summary>
    public Action<Exception, IPEndPoint>? ExceptionHandler { get; set; }
}
