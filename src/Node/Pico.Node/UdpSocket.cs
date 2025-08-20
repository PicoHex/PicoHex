namespace Pico.Node;

/// <summary>
/// High-performance UDP socket implementation using SocketAsyncEventArgs for optimal performance
/// </summary>
public sealed class UdpSocket : IDisposable
{
    private readonly Socket _socket;
    private readonly SocketAsyncEventArgs _receiveEventArgs;
    private readonly SocketAsyncEventArgs _sendEventArgs;
    private readonly byte[] _receiveBuffer;
    private readonly object _sendLock = new object();
    private bool _disposed;

    /// <summary>
    /// Event raised when data is received
    /// </summary>
    public event Action<ReadOnlyMemory<byte>, EndPoint> DataReceived;

    /// <summary>
    /// Local endpoint the socket is bound to
    /// </summary>
    public IPEndPoint LocalEndPoint { get; private set; }

    /// <summary>
    /// Creates a new high-performance UDP socket
    /// </summary>
    /// <param name="bufferSize">Size of the receive buffer (default: 8192 bytes)</param>
    public UdpSocket(int bufferSize = 8192)
    {
        _socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);

        // Configure socket for high performance
        _socket.ReceiveBufferSize = bufferSize * 2;
        _socket.SendBufferSize = bufferSize * 2;

        // Create receive buffer
        _receiveBuffer = new byte[bufferSize];

        // Set up receive event args
        _receiveEventArgs = new SocketAsyncEventArgs();
        _receiveEventArgs.SetBuffer(_receiveBuffer, 0, _receiveBuffer.Length);
        _receiveEventArgs.Completed += OnReceiveCompleted;

        // Set up send event args
        _sendEventArgs = new SocketAsyncEventArgs();
        _sendEventArgs.Completed += OnSendCompleted;

        // Start receiving
        StartReceive();
    }

    /// <summary>
    /// Binds the socket to the specified local endpoint
    /// </summary>
    /// <param name="localEndPoint">Local endpoint to bind to</param>
    public void Bind(IPEndPoint localEndPoint)
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(UdpSocket));

        _socket.Bind(localEndPoint);
        LocalEndPoint = (IPEndPoint)_socket.LocalEndPoint;
    }

    /// <summary>
    /// Starts the asynchronous receive operation
    /// </summary>
    private void StartReceive()
    {
        if (_disposed)
            return;

        try
        {
            // Set remote endpoint to any for receiving
            _receiveEventArgs.RemoteEndPoint = new IPEndPoint(IPAddress.Any, 0);

            // Start async receive if not already in progress
            if (!_socket.ReceiveAsync(_receiveEventArgs))
            {
                // Operation completed synchronously, process it
                ProcessReceive(_receiveEventArgs);
            }
        }
        catch (ObjectDisposedException)
        {
            // Socket was disposed, ignore
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error starting receive: {ex.Message}");
        }
    }

    /// <summary>
    /// Handles completion of a receive operation
    /// </summary>
    private void OnReceiveCompleted(object sender, SocketAsyncEventArgs e)
    {
        ProcessReceive(e);
    }

    /// <summary>
    /// Processes a completed receive operation
    /// </summary>
    private void ProcessReceive(SocketAsyncEventArgs e)
    {
        if (_disposed)
            return;

        try
        {
            if (e is { SocketError: SocketError.Success, BytesTransferred: > 0 })
            {
                // Extract received data
                var data = new ReadOnlyMemory<byte>(_receiveBuffer, 0, e.BytesTransferred);

                // Raise DataReceived event
                DataReceived?.Invoke(data, e.RemoteEndPoint);
            }

            // Restart receive operation
            StartReceive();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error processing receive: {ex.Message}");

            // Restart receive even if there was an error
            if (!_disposed)
            {
                StartReceive();
            }
        }
    }

    /// <summary>
    /// Asynchronously sends data to the specified remote endpoint
    /// </summary>
    /// <param name="data">Data to send</param>
    /// <param name="remoteEndPoint">Remote endpoint to send to</param>
    public ValueTask SendAsync(ReadOnlyMemory<byte> data, IPEndPoint remoteEndPoint)
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(UdpSocket));

        var tcs = new TaskCompletionSource<bool>();

        // Rent a buffer for the send operation
        var buffer = ArrayPool<byte>.Shared.Rent(data.Length);
        data.CopyTo(buffer);

        // Set up send event args
        var sendArgs = new SocketAsyncEventArgs();
        sendArgs.SetBuffer(buffer, 0, data.Length);
        sendArgs.RemoteEndPoint = remoteEndPoint;
        sendArgs.UserToken = buffer; // Store buffer for cleanup
        sendArgs.Completed += (s, e) =>
        {
            // Return buffer to pool
            if (e.UserToken is byte[] buf)
            {
                ArrayPool<byte>.Shared.Return(buf);
            }

            if (e.SocketError == SocketError.Success)
            {
                tcs.TrySetResult(true);
            }
            else
            {
                tcs.TrySetException(new SocketException((int)e.SocketError));
            }

            e.Dispose();
        };

        // Start async send
        if (!_socket.SendAsync(sendArgs))
        {
            // Operation completed synchronously
            if (sendArgs.UserToken is byte[] buf)
            {
                ArrayPool<byte>.Shared.Return(buf);
            }

            if (sendArgs.SocketError == SocketError.Success)
            {
                tcs.TrySetResult(true);
            }
            else
            {
                tcs.TrySetException(new SocketException((int)sendArgs.SocketError));
            }

            sendArgs.Dispose();
        }

        return new ValueTask(tcs.Task);
    }

    /// <summary>
    /// Handles completion of a send operation
    /// </summary>
    private void OnSendCompleted(object sender, SocketAsyncEventArgs e)
    {
        // This method is not used with the current implementation
        // as we create a new SocketAsyncEventArgs for each send operation
    }

    /// <summary>
    /// Releases all resources used by the socket
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;

        try
        {
            _socket.Shutdown(SocketShutdown.Both);
        }
        catch
        {
            // Ignore shutdown errors
        }

        _socket.Close();
        _socket.Dispose();

        _receiveEventArgs.Dispose();
        _sendEventArgs.Dispose();
    }
}
