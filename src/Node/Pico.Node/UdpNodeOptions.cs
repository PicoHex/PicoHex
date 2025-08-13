namespace Pico.Node;

public sealed class UdpNodeOptions
{
    public required IPAddress IpAddress { get; init; }
    public required ushort Port { get; init; }
    public Action<Exception, IPEndPoint>? ExceptionHandler { get; init; }
    public int MaxConcurrency { get; init; } = 1000;
    public Action<UdpClient>? ConfigureUdpClient { get; init; }
    public int MaxQueueSize { get; init; } = 5000;
    public TimeSpan StopTasksTimeout { get; init; } = TimeSpan.FromSeconds(5);
    public TimeSpan DisposeServerTaskTimeout { get; init; } = TimeSpan.FromSeconds(5);
    public TimeSpan DisposeProcessingTasksTimeout { get; init; } = TimeSpan.FromSeconds(5);
}
