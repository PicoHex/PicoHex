namespace Pico.Trsp;

public interface ITransportLayerProtocol
{
    ProtocolType ProtocolType { get; }
    ushort SourcePort { get; set; }
    ushort DestinationPort { get; set; }
    IPEndPoint LocalEndPoint { get; }
    IPEndPoint RemoteEndPoint { get; }

    event Action<byte[]> DataReceived;
    event Action<Exception> ErrorOccurred;

    void Bind(IPEndPoint localEP);
    void Send(byte[] data, PacketPriority priority = PacketPriority.Normal);
    void Close(TimeSpan? lingerTimeout = null);

    // Statistics
    long TotalBytesSent { get; }
    long TotalBytesReceived { get; }
    TimeSpan Uptime { get; }
}

public enum PacketPriority
{
    Immediate,
    High,
    Normal,
    Low
}
