namespace Pico.Node;

/// <summary>
/// TCP client connection
/// </summary>
public class TcpClientConnection : IDisposable
{
    public Guid Id { get; } = Guid.NewGuid();
    public Socket Socket { get; }
    public IPEndPoint RemoteEndPoint { get; }

    private bool _disposed = false;

    public TcpClientConnection(Socket socket)
    {
        Socket = socket;
        RemoteEndPoint = (IPEndPoint)socket.RemoteEndPoint;
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        try
        {
            Socket.Shutdown(SocketShutdown.Both);
            Socket.Close();
        }
        catch
        {
            // Ignore close errors
        }
    }
}
