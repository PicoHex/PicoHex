namespace PicoHex.Tcp.Server;

/// <summary>
/// A high-performance asynchronous TCP server implementation supporting massive concurrent connections.
/// </summary>
public sealed class AsyncTcpServer : IAsyncDisposable
{
    // Core server components
    private readonly Socket _listener;
    private readonly CancellationTokenSource _cts = new();
    private readonly ConcurrentDictionary<Guid, Socket> _connections = new();
    private readonly SemaphoreSlim _connectionLock = new(1, 1);

    // Server state tracking
    private bool _disposed;
    private long _totalConnections;
    private long _totalBytesReceived;

    // Performance configuration constants
    private const int MaxConnections = 100_000;
    private const int ReceiveBufferSize = 16 * 1024;
    private const int SendBufferSize = 16 * 1024;
    private const int Backlog = 4096;
    private const int MaxSimultaneousAccept = 16;

    /// <summary>
    /// Initializes a new TCP server instance bound to the specified endpoint.
    /// </summary>
    /// <param name="endPoint">The network endpoint to listen on</param>
    public AsyncTcpServer(IPEndPoint endPoint)
    {
        _listener = new Socket(endPoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);

        // Configure socket for high performance
        _listener.NoDelay = true; // Disable Nagle's algorithm
        _listener.LingerState = new LingerOption(false, 0); // Abort connections on close
        _listener.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, 1);
        _listener.ReceiveBufferSize = ReceiveBufferSize;
        _listener.SendBufferSize = SendBufferSize;

        _listener.Bind(endPoint);
        _listener.Listen(Backlog);
    }

    /// <summary>
    /// Starts the TCP server and begins accepting incoming connections.
    /// </summary>
    /// <exception cref="System.Net.Sockets.SocketException">Thrown when socket initialization fails</exception>
    public async Task StartAsync()
    {
        try
        {
            var acceptTasks = new List<Task>(MaxSimultaneousAccept);
            for (var i = 0; i < MaxSimultaneousAccept; i++)
            {
                acceptTasks.Add(AcceptConnectionsAsync());
            }
            await Task.WhenAll(acceptTasks);
        }
        catch (Exception ex)
        {
            LogError("Server fatal error", ex);
            throw;
        }
    }

    /// <summary>
    /// Core connection acceptance loop handling incoming connections
    /// </summary>
    private async Task AcceptConnectionsAsync()
    {
        while (!_cts.IsCancellationRequested)
        {
            try
            {
                var clientSocket = await _listener.AcceptAsync(_cts.Token);

                // Enforce maximum connection limit
                if (Interlocked.Read(ref _totalConnections) >= MaxConnections)
                {
                    await CloseConnectionImmediately(clientSocket, "Max connections reached");
                    continue;
                }

                var connectionId = Guid.NewGuid();
                _connections.TryAdd(connectionId, clientSocket);
                Interlocked.Increment(ref _totalConnections);

                // Start pipeline processing with proper error handling
                _ = ProcessClientWithPipelineAsync(clientSocket, connectionId)
                    .ContinueWith(
                        t => HandleTaskException(t, connectionId),
                        TaskContinuationOptions.OnlyOnFaulted
                    );
            }
            catch (OperationCanceledException)
            {
                // Normal shutdown path
                break;
            }
            catch (SocketException ex) when (IsIgnorableSocketError(ex.SocketErrorCode))
            {
                LogWarning($"Socket error during accept: {ex.SocketErrorCode}");
            }
            catch (Exception ex)
            {
                LogError("Accept error", ex);
            }
        }
    }

    /// <summary>
    /// Processes a client connection using System.IO.Pipelines for efficient data handling
    /// </summary>
    /// <param name="socket">The client socket</param>
    /// <param name="connectionId">Unique connection identifier</param>
    private async Task ProcessClientWithPipelineAsync(Socket socket, Guid connectionId)
    {
        try
        {
            using (socket)
            {
                var pipe = new Pipe();
                var processingTask = ProcessPipeAsync(pipe.Reader, socket, connectionId);
                var writingTask = FillPipeAsync(socket, pipe.Writer);

                await Task.WhenAll(processingTask, writingTask);
            }
        }
        finally
        {
            // Cleanup connection tracking
            _connections.TryRemove(connectionId, out _);
            Interlocked.Decrement(ref _totalConnections);
        }
    }

    /// <summary>
    /// Fills the pipeline with data from the socket
    /// </summary>
    private async Task FillPipeAsync(Socket socket, PipeWriter writer)
    {
        const int minimumBufferSize = 512;

        try
        {
            while (!_cts.IsCancellationRequested)
            {
                Memory<byte> memory = writer.GetMemory(minimumBufferSize);

                int bytesRead = await socket
                    .ReceiveAsync(memory, SocketFlags.None, _cts.Token)
                    .ConfigureAwait(false);

                if (bytesRead == 0)
                    break;

                Interlocked.Add(ref _totalBytesReceived, bytesRead);
                writer.Advance(bytesRead);

                FlushResult result = await writer.FlushAsync(_cts.Token).ConfigureAwait(false);

                if (result.IsCompleted || result.IsCanceled)
                    break;
            }
        }
        catch (Exception ex) when (ShouldIgnoreException(ex))
        {
            // Ignore expected exceptions during shutdown
        }
        finally
        {
            await writer.CompleteAsync().ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Processes data from the pipeline reader
    /// </summary>
    private async Task ProcessPipeAsync(PipeReader reader, Socket socket, Guid connectionId)
    {
        try
        {
            while (!_cts.IsCancellationRequested)
            {
                ReadResult result = await reader.ReadAsync(_cts.Token).ConfigureAwait(false);
                ReadOnlySequence<byte> buffer = result.Buffer;

                try
                {
                    if (buffer.IsEmpty && result.IsCompleted)
                        break;

                    await ProcessDataAsync(buffer, socket, connectionId);
                }
                finally
                {
                    reader.AdvanceTo(buffer.End);
                }
            }
        }
        catch (Exception ex) when (ShouldIgnoreException(ex))
        {
            // Ignore expected exceptions during shutdown
        }
        finally
        {
            await reader.CompleteAsync().ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Processes received data using memory pooling
    /// </summary>
    private async ValueTask ProcessDataAsync(
        ReadOnlySequence<byte> buffer,
        Socket socket,
        Guid connectionId
    )
    {
        LogInfo($"Processing data for connection {connectionId}");
        // Optimize for single-segment buffers
        if (buffer.IsSingleSegment)
        {
            await ProcessSingleSegment(buffer.First, socket);
        }
        else
        {
            // Use pooled memory for multi-segment buffers
            var pooledArray = ArrayPool<byte>.Shared.Rent((int)buffer.Length);
            try
            {
                buffer.CopyTo(pooledArray);
                await ProcessSingleSegment(
                    new ReadOnlyMemory<byte>(pooledArray, 0, (int)buffer.Length),
                    socket
                );
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(pooledArray);
            }
        }
    }

    /// <summary>
    /// Processes a single memory segment (echo implementation)
    /// </summary>
    private async ValueTask ProcessSingleSegment(ReadOnlyMemory<byte> data, Socket socket)
    {
        try
        {
            // Example echo implementation
            await socket.SendAsync(data, SocketFlags.None, _cts.Token).ConfigureAwait(false);
        }
        catch (Exception ex) when (ShouldIgnoreException(ex))
        {
            // Ignore expected send failures
        }
    }

    #region Exception Handling and Resource Management
    /// <summary>
    /// Determines if an exception represents an expected error condition
    /// </summary>
    private bool ShouldIgnoreException(Exception ex)
    {
        return ex is OperationCanceledException
            || ex is SocketException { SocketErrorCode: SocketError.OperationAborted }
            || ex is ObjectDisposedException;
    }

    /// <summary>
    /// Identifies socket errors that can be safely ignored
    /// </summary>
    private bool IsIgnorableSocketError(SocketError errorCode)
    {
        return errorCode switch
        {
            SocketError.ConnectionReset => true, // Client disconnected
            SocketError.OperationAborted => true, // Server shutdown
            SocketError.Shutdown => true, // Graceful closure
            _ => false
        };
    }

    /// <summary>
    /// Immediately closes a connection with specified reason
    /// </summary>
    private static async ValueTask CloseConnectionImmediately(Socket socket, string reason)
    {
        try
        {
            LogWarning($"Closing connection: {reason}");
            await socket.DisconnectAsync(reuseSocket: false);
            socket.Close();
        }
        catch (Exception ex)
        {
            LogError("Error closing connection", ex);
        }
    }

    /// <summary>
    /// Handles exceptions from processing tasks
    /// </summary>
    private void HandleTaskException(Task task, Guid connectionId)
    {
        if (task.Exception != null)
        {
            LogError($"Connection {connectionId} failed", task.Exception);
            if (_connections.TryRemove(connectionId, out var socket))
            {
                _ = CloseConnectionImmediately(socket, "Task faulted");
            }
        }
    }
    #endregion

    #region Dispose Implementation
    /// <summary>
    /// Asynchronously releases all resources used by the TCP server
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;
        _disposed = true;

        try
        {
            await _cts.CancelAsync();

            // Close listener socket
            await Task.Run(() =>
                {
                    try
                    {
                        _listener.Shutdown(SocketShutdown.Both);
                    }
                    catch
                    {
                        // Ignore shutdown errors
                    }
                    _listener.Dispose();
                })
                .ConfigureAwait(false);

            // Close all active connections in parallel
            var closeTasks = new List<Task>(_connections.Count);
            closeTasks.AddRange(
                _connections.Values.Select(socket =>
                    Task.Run(() =>
                    {
                        try
                        {
                            socket.Shutdown(SocketShutdown.Both);
                            socket.Dispose();
                        }
                        catch
                        {
                            // Ignore closure errors
                        }
                    })
                )
            );

            await Task.WhenAll(closeTasks).ConfigureAwait(false);
            _connections.Clear();
        }
        catch (Exception ex)
        {
            LogError("Dispose error", ex);
        }
        finally
        {
            _cts.Dispose();
            _connectionLock.Dispose();
        }
    }
    #endregion

    #region Logging Methods
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void LogError(string message, Exception? ex = null)
    {
        // Implement actual logging here
        Console.WriteLine($"[ERROR] {message}: {ex?.ToString() ?? ""}");
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void LogWarning(string message)
    {
        // Implement actual logging here
        Console.WriteLine($"[WARN] {message}");
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void LogInfo(string message)
    {
        // Implement actual logging here
        Console.WriteLine($"[INFO] {message}");
    }
    #endregion

    #region Server Metrics
    /// <summary>
    /// Gets the total number of connections handled since server start
    /// </summary>
    public long TotalConnections => Interlocked.Read(ref _totalConnections);

    /// <summary>
    /// Gets the current number of active connections
    /// </summary>
    public long CurrentConnections => _connections.Count;

    /// <summary>
    /// Gets the total number of bytes received by the server
    /// </summary>
    public long TotalBytesReceived => Interlocked.Read(ref _totalBytesReceived);
    #endregion
}
