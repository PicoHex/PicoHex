namespace Pico.Node;

public class UdpNodeOptions
{
    public IPAddress IpAddress { get; init; } = IPAddress.Any;
    public int Port { get; init; } = 1337;
    public int MaxConcurrency { get; set; } = Math.Max(4, Environment.ProcessorCount * 2);
    public int MaxQueueSize { get; set; } = 1024;
    public TimeSpan StopTasksTimeout { get; set; } = TimeSpan.FromSeconds(5);
    public TimeSpan DisposeServerTaskTimeout { get; set; } = TimeSpan.FromSeconds(5);
    public TimeSpan DisposeProcessingTasksTimeout { get; set; } = TimeSpan.FromSeconds(10);
    public Action<Socket>? ConfigureSocket { get; set; }
    public int ReceiveBufferSize { get; set; } = 65536;
    public Action<Exception, IPEndPoint>? ExceptionHandler { get; set; }
}
