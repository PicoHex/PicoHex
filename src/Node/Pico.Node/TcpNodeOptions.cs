namespace Pico.Node;

public sealed class TcpNodeOptions
{
    public required IPAddress IpAddress { get; init; }

    public required ushort Port { get; init; }

    public int MaxConcurrentConnections { get; init; } = 100;

    public TimeSpan ServerStopTimeout { get; init; } = TimeSpan.FromSeconds(2);

    public TimeSpan ClientStopTimeout { get; init; } = TimeSpan.FromSeconds(5);

    public Action<Exception, IPEndPoint>? ExceptionHandler { get; init; }
}
