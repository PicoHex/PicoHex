namespace PicoHex.Transport;

public class UdpProtocol : IUdpProtocol
{
    private Socket _socket;
    private long _totalBytesSent;
    private long _totalBytesReceived;
    private Stopwatch _uptimeStopwatch;
    private bool _disposed;

    public event Action<byte[]> DataReceived;
    public event Action<Exception> ErrorOccurred;

    public ProtocolType ProtocolType => ProtocolType.Udp;
    public ushort SourcePort
    {
        get => (ushort)LocalEndPoint.Port;
        set => LocalEndPoint.Port = value;
    }
    public ushort DestinationPort { get; set; }
    public IPEndPoint LocalEndPoint { get; private set; }

    public IPEndPoint RemoteEndPoint { get; }

    public int MaxDatagramSize => 65507;

    public bool IsBroadcastEnabled
    {
        get => _socket.EnableBroadcast;
        set => _socket.EnableBroadcast = value;
    }

    public bool IsMulticastEnabled { get; set; }
    public bool IgnoreICMPErrors { get; set; }
    public long TotalBytesSent => _totalBytesSent;
    public long TotalBytesReceived => _totalBytesReceived;
    public TimeSpan Uptime => _uptimeStopwatch?.Elapsed ?? TimeSpan.Zero;

    public void Bind(IPEndPoint localEP)
    {
        if (_socket != null)
            throw new InvalidOperationException("Already bound");

        _socket = new Socket(localEP.AddressFamily, SocketType.Dgram, ProtocolType.Udp);
        _socket.Bind(localEP);
        LocalEndPoint = (IPEndPoint)_socket.LocalEndPoint;
        _uptimeStopwatch = Stopwatch.StartNew();

        _ = Task.Run(ReceiveLoop);
    }

    private async Task ReceiveLoop()
    {
        var buffer = new byte[MaxDatagramSize];

        while (_socket is { IsBound: true })
        {
            try
            {
                var receiveResult = await _socket.ReceiveFromAsync(
                    new ArraySegment<byte>(buffer),
                    SocketFlags.None,
                    new IPEndPoint(
                        _socket.AddressFamily == AddressFamily.InterNetwork
                            ? IPAddress.Any
                            : IPAddress.IPv6Any,
                        0
                    )
                );

                if (receiveResult.ReceivedBytes > 0)
                {
                    Interlocked.Add(ref _totalBytesReceived, receiveResult.ReceivedBytes);
                    var data = new byte[receiveResult.ReceivedBytes];
                    Buffer.BlockCopy(buffer, 0, data, 0, data.Length);
                    DataReceived?.Invoke(data);
                }
            }
            catch (Exception ex) when (IsIgnorableException(ex))
            {
                ErrorOccurred?.Invoke(ex);
                break;
            }
        }
    }

    private bool IsIgnorableException(Exception ex)
    {
        return ex is ObjectDisposedException
            || (
                IgnoreICMPErrors
                && ex is SocketException { SocketErrorCode: SocketError.ConnectionReset }
            );
    }

    public void Send(byte[] data, PacketPriority priority = PacketPriority.Normal)
    {
        if (RemoteEndPoint == null)
            throw new InvalidOperationException("Remote endpoint not set");
        SendTo(data, RemoteEndPoint);
    }

    public void SendTo(byte[] datagram, IPEndPoint remoteEP)
    {
        if (_socket == null)
            throw new InvalidOperationException("Not bound");
        _socket.SendTo(datagram, remoteEP);
        Interlocked.Add(ref _totalBytesSent, datagram.Length);
    }

    public async Task<UdpReceiveResult> ReceiveAsync()
    {
        var buffer = new byte[MaxDatagramSize];
        var result = await _socket.ReceiveFromAsync(
            new ArraySegment<byte>(buffer),
            SocketFlags.None,
            new IPEndPoint(
                _socket.AddressFamily == AddressFamily.InterNetwork
                    ? IPAddress.Any
                    : IPAddress.IPv6Any,
                0
            )
        );

        return new UdpReceiveResult
        {
            Buffer = buffer.AsMemory(0, result.ReceivedBytes).ToArray(),
            RemoteEndPoint = (IPEndPoint)result.RemoteEndPoint
        };
    }

    public void JoinMulticastGroup(IPAddress multicastAddress)
    {
        if (_socket == null)
            throw new InvalidOperationException("Not bound");
        _socket.SetSocketOption(
            SocketOptionLevel.IP,
            SocketOptionName.AddMembership,
            new MulticastOption(multicastAddress)
        );
        IsMulticastEnabled = true;
    }

    public void LeaveMulticastGroup(IPAddress multicastAddress)
    {
        if (_socket == null)
            throw new InvalidOperationException("Not bound");
        _socket.SetSocketOption(
            SocketOptionLevel.IP,
            SocketOptionName.DropMembership,
            new MulticastOption(multicastAddress)
        );
        IsMulticastEnabled = false;
    }

    public void Close(TimeSpan? lingerTimeout = null)
    {
        if (_socket == null)
            return;

        if (lingerTimeout.HasValue)
            _socket.LingerState = new LingerOption(true, (int)lingerTimeout.Value.TotalSeconds);

        _socket.Dispose();
        _socket = null;
        _uptimeStopwatch?.Stop();
    }

    public void Dispose() => Close();
}
