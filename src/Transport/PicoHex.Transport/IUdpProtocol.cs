namespace PicoHex.Transport;

public interface IUdpProtocol : ITransportLayerProtocol
{
    // Datagram features
    int MaxDatagramSize { get; }
    bool IsBroadcastEnabled { get; set; }
    bool IsMulticastEnabled { get; set; }

    // Multicast management
    void JoinMulticastGroup(IPAddress multicastAddress);
    void LeaveMulticastGroup(IPAddress multicastAddress);

    // Connectionless operations
    void SendTo(byte[] datagram, IPEndPoint remoteEP);
    Task<UdpReceiveResult> ReceiveAsync();

    // Error handling
    bool IgnoreICMPErrors { get; set; }
}

public struct UdpReceiveResult
{
    public byte[] Buffer;
    public IPEndPoint RemoteEndPoint;
}
