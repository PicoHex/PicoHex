namespace Pico.Node;

/// <summary>
/// High-performance TCP node
/// </summary>
public class TcpNode : IDisposable
{
    private readonly Socket _listenerSocket;
    private readonly ITcpHandler _handler;
    private readonly CancellationTokenSource _cts = new();
    private readonly ConcurrentDictionary<Guid, TcpClientConnection> _connections = new();
    private bool _disposed;
    private bool _listening;

    /// <summary>
    /// Creates a high-performance TCP node
    /// </summary>
    public TcpNode(ITcpHandler handler, AddressFamily addressFamily = AddressFamily.InterNetwork)
    {
        _handler = handler ?? throw new ArgumentNullException(nameof(handler));
        _listenerSocket = new Socket(addressFamily, SocketType.Stream, ProtocolType.Tcp);

        // Configure high-performance options
        _listenerSocket.NoDelay = true; // Disable Nagle's algorithm
        _listenerSocket.LingerState = new LingerOption(false, 0); // Disable lingering close
    }

    /// <summary>
    /// Starts listening
    /// </summary>
    public async Task StartAsync(IPEndPoint localEndPoint, int backlog = 100)
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(TcpNode));
        if (_listening)
            throw new InvalidOperationException("Already listening");

        try
        {
            _listenerSocket.Bind(localEndPoint);
            _listenerSocket.Listen(backlog);
            _listening = true;

            // Start accepting client connections
            _ = Task.Run(() => AcceptClientsAsync(_cts.Token), _cts.Token);

            Console.WriteLine($"High-performance TCP server started on {localEndPoint}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error starting TCP server: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// Asynchronously accepts client connections
    /// </summary>
    private async Task AcceptClientsAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                Socket clientSocket = await _listenerSocket.AcceptAsync(cancellationToken);

                // Configure client socket
                clientSocket.NoDelay = true;
                clientSocket.LingerState = new LingerOption(false, 0);

                // Create client connection and process
                var connection = new TcpClientConnection(clientSocket);
                if (_connections.TryAdd(connection.Id, connection))
                {
                    // Process connection in background
                    _ = Task.Run(
                        () => ProcessConnectionAsync(connection, cancellationToken),
                        cancellationToken
                    );
                }
            }
            catch (OperationCanceledException)
            {
                // Normal cancellation
                break;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error accepting client: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Processes client connection (using System.IO.Pipelines)
    /// </summary>
    private async Task ProcessConnectionAsync(
        TcpClientConnection connection,
        CancellationToken cancellationToken
    )
    {
        // Create Pipe for efficient reading
        var pipe = new Pipe();
        var writing = FillPipeAsync(connection.Socket, pipe.Writer, cancellationToken);
        var reading = ReadPipeAsync(pipe.Reader, connection, cancellationToken);

        // Wait for both reading and writing tasks to complete
        await Task.WhenAll(reading, writing);

        // Clean up connection
        _connections.TryRemove(connection.Id, out _);
        connection.Dispose();
    }

    /// <summary>
    /// Reads data from socket and writes to Pipe
    /// </summary>
    private async Task FillPipeAsync(
        Socket socket,
        PipeWriter writer,
        CancellationToken cancellationToken
    )
    {
        const int minimumBufferSize = 4096;

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                // Get memory from PipeWriter
                Memory<byte> memory = writer.GetMemory(minimumBufferSize);

                // Read data from socket
                int bytesRead = await socket.ReceiveAsync(
                    memory,
                    SocketFlags.None,
                    cancellationToken
                );
                if (bytesRead == 0)
                {
                    break; // Connection closed
                }

                // Tell PipeWriter how much data we've written
                writer.Advance(bytesRead);

                // Make data available to PipeReader
                FlushResult result = await writer.FlushAsync(cancellationToken);

                if (result.IsCompleted)
                {
                    break;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error reading from socket: {ex.Message}");
                break;
            }
        }

        // Tell PipeReader there's no more data
        await writer.CompleteAsync();
    }

    /// <summary>
    /// Reads data from Pipe and processes it
    /// </summary>
    private async Task ReadPipeAsync(
        PipeReader reader,
        TcpClientConnection connection,
        CancellationToken cancellationToken
    )
    {
        // Create a NetworkStream from the socket and then create a PipeWriter from the stream
        await using var networkStream = new NetworkStream(connection.Socket, ownsSocket: false);
        var pipeWriter = PipeWriter.Create(networkStream);

        try
        {
            // Call handler to process connection
            await _handler.HandleAsync(
                reader,
                pipeWriter,
                connection.RemoteEndPoint,
                cancellationToken
            );
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error processing TCP connection: {ex.Message}");
        }
        finally
        {
            // Complete reading and writing
            await reader.CompleteAsync();
            await pipeWriter.CompleteAsync();
        }
    }

    /// <summary>
    /// Stops the server
    /// </summary>
    public void Stop()
    {
        if (!_listening)
            return;

        _listening = false;

        // Close all connections
        foreach (var connection in _connections.Values)
        {
            connection.Dispose();
        }
        _connections.Clear();

        // Close listener socket
        try
        {
            _listenerSocket.Close();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error stopping TCP server: {ex.Message}");
        }
    }

    /// <summary>
    /// Releases resources
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _cts.Cancel();
        Stop();
        _cts.Dispose();
        _listenerSocket.Dispose();
        GC.SuppressFinalize(this);
    }
}
