namespace Pico.Trsp;

public interface ITcpProtocol : ITransportLayerProtocol
{
    // Connection management
    TcpState State { get; }
    void Listen(int backlog);
    Task ConnectAsync(IPEndPoint remoteEP, CancellationToken cancellationToken = default);
    void Disconnect(bool abortive = false);

    // Reliability mechanisms
    uint SequenceNumber { get; }
    uint AcknowledgmentNumber { get; }
    TimeSpan RetransmissionTimeout { get; set; }

    // Flow control
    int ReceiveWindowSize { get; set; }
    int SendWindowSize { get; set; }
    void Acknowledge(uint ackNumber);

    // Congestion control
    TcpCongestionAlgorithm CongestionAlgorithm { get; set; }
    int CongestionWindow { get; }
    int SlowStartThreshold { get; }

    // Advanced features
    void EnableNagleAlgorithm(bool enable);
    void EnableSelectiveAck(bool enable);
    void SetKeepAlive(TimeSpan interval, TimeSpan timeout);
}

public enum TcpCongestionAlgorithm
{
    Reno,
    Cubic,
    BBR,
    NewReno
}

public enum TcpState
{
    Closed,
    Listen,
    SynSent,
    SynReceived,
    Established,
    CloseWait,
    LastAck
}
