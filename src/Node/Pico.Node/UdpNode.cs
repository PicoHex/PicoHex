namespace Pico.Node;

/// <summary>
/// 端点比较器（支持各种EndPoint类型）
/// </summary>
public sealed class EndPointComparer : IEqualityComparer<EndPoint>
{
    public bool Equals(EndPoint? x, EndPoint? y)
    {
        if (ReferenceEquals(x, y))
            return true;
        if (x is null || y is null)
            return false;

        return x switch
        {
            IPEndPoint ipX when y is IPEndPoint ipY
                => ipX.Address.Equals(ipY.Address) && ipX.Port == ipY.Port,
            DnsEndPoint dnsX when y is DnsEndPoint dnsY
                => dnsX.Host == dnsY.Host && dnsX.Port == dnsY.Port,
            _ => x.Equals(y)
        };
    }

    public int GetHashCode(EndPoint obj)
    {
        return obj switch
        {
            IPEndPoint ip => HashCode.Combine(ip.Address, ip.Port),
            DnsEndPoint dns => HashCode.Combine(dns.Host, dns.Port),
            _ => obj.GetHashCode()
        };
    }
}

/// <summary>
/// UDP节点实现（优化修复版）
/// </summary>
public sealed class UdpNode : INode
{
    private readonly int _maxConcurrency;
    private readonly Func<IUdpHandler> _handlerFactory;
    private readonly SemaphoreSlim _concurrencyLimiter;

    private readonly ConcurrentDictionary<EndPoint, SemaphoreSlim> _endpointSendLocks;
    private readonly EndPointComparer _endpointComparer = new();

    private Socket? _socket;
    private CancellationTokenSource? _cts;
    private Task? _receiveTask;
    private bool _disposed;

    // 活动任务跟踪
    private readonly ConcurrentDictionary<Task, bool> _activeTasks = new();
    private readonly Lock _taskTrackingLock = new();

    private readonly IPAddress _listenAddress;
    private readonly int _port;

    public UdpNode(
        IPAddress listenAddress,
        int port,
        Func<IUdpHandler> handlerFactory,
        int maxConcurrency = 1000
    )
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxConcurrency);

        _maxConcurrency = maxConcurrency;
        _handlerFactory = handlerFactory ?? throw new ArgumentNullException(nameof(handlerFactory));
        _concurrencyLimiter = new SemaphoreSlim(maxConcurrency, maxConcurrency);
        _endpointSendLocks = new ConcurrentDictionary<EndPoint, SemaphoreSlim>(_endpointComparer);

        _listenAddress = listenAddress;
        _port = port;
    }

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (_receiveTask != null)
            throw new InvalidOperationException("Node already started");

        _cts = new CancellationTokenSource();
        var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
            _cts.Token,
            cancellationToken
        );

        try
        {
            // 根据IP地址类型创建正确的Socket
            _socket = new Socket(_listenAddress.AddressFamily, SocketType.Dgram, ProtocolType.Udp);

            // 修复绑定问题：使用正确的IPEndPoint
            _socket.Bind(new IPEndPoint(_listenAddress, _port));

            _receiveTask = RunReceiveLoop(linkedCts.Token);
            await Task.Yield();
        }
        catch
        {
            CleanupResources();
            throw;
        }
    }

    private async Task RunReceiveLoop(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            // 使用内存池租用缓冲区
            using var bufferOwner = MemoryPool<byte>.Shared.Rent(65507);
            var memory = bufferOwner.Memory;

            // 根据监听地址类型创建正确的端点
            EndPoint remoteEndPoint =
                _listenAddress.AddressFamily == AddressFamily.InterNetworkV6
                    ? new IPEndPoint(IPAddress.IPv6Any, 0)
                    : new IPEndPoint(IPAddress.Any, 0);

            try
            {
                var result = await _socket!.ReceiveFromAsync(
                    memory,
                    SocketFlags.None,
                    remoteEndPoint,
                    cancellationToken
                );

                // 获取信号量（控制并发）
                await _concurrencyLimiter.WaitAsync(cancellationToken);

                // 切片实际数据（零拷贝）
                var receivedData = memory[..result.ReceivedBytes];

                // 启动处理任务（跟踪活动任务）
                var processTask = ProcessDatagram(
                    bufferOwner,
                    receivedData,
                    result.RemoteEndPoint,
                    cancellationToken
                );

                TrackTask(processTask);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (SocketException ex) when (ex.SocketErrorCode == SocketError.OperationAborted)
            {
                break;
            }
            catch (ObjectDisposedException)
            {
                break;
            }
            finally
            {
                // 在异常情况下确保释放缓冲区
                if (!cancellationToken.IsCancellationRequested)
                    bufferOwner.Dispose();
            }
        }
    }

    private void TrackTask(Task task)
    {
        lock (_taskTrackingLock)
        {
            _activeTasks[task] = true;
            task.ContinueWith(
                t =>
                {
                    lock (_taskTrackingLock)
                    {
                        _activeTasks.TryRemove(t, out _);
                    }
                },
                TaskScheduler.Default
            );
        }
    }

    private async Task ProcessDatagram(
        IMemoryOwner<byte> bufferOwner, // 保持缓冲区所有权
        ReadOnlyMemory<byte> data,
        EndPoint remoteEndPoint,
        CancellationToken cancellationToken
    )
    {
        try
        {
            var handler = _handlerFactory();

            // 获取端点级发送锁
            var sendLock = _endpointSendLocks.GetOrAdd(
                remoteEndPoint,
                _ => new SemaphoreSlim(1, 1)
            );

            // 响应发送函数（端点级锁）
            async ValueTask SendResponse(ReadOnlyMemory<byte> responseData)
            {
                await sendLock.WaitAsync(cancellationToken);
                try
                {
                    await _socket!.SendToAsync(
                        responseData,
                        SocketFlags.None,
                        remoteEndPoint,
                        cancellationToken
                    );
                }
                finally
                {
                    sendLock.Release();
                }
            }

            try
            {
                // 修复接口匹配问题
                await handler.HandleAsync(data, remoteEndPoint, SendResponse, cancellationToken);
            }
            finally
            {
                // 释放处理器资源
                if (handler is IDisposable disposable)
                    disposable.Dispose();
                if (handler is IAsyncDisposable asyncDisposable)
                    await asyncDisposable.DisposeAsync();

                // 显式释放缓冲区
                bufferOwner.Dispose();
            }
        }
        finally
        {
            _concurrencyLimiter.Release();
        }
    }

    public async Task StopAsync(TimeSpan? timeout = null)
    {
        if (_receiveTask == null)
            return;

        // 触发取消
        await _cts?.CancelAsync()!;

        // 等待接收循环停止
        var timeoutValue = timeout ?? TimeSpan.FromSeconds(5);
        await _receiveTask.WaitAsync(timeoutValue).ConfigureAwait(false);

        // 等待所有处理任务完成
        Task[] tasksToWait;
        lock (_taskTrackingLock)
        {
            tasksToWait = _activeTasks.Keys.ToArray();
        }

        if (tasksToWait.Length > 0)
        {
            await Task.WhenAll(tasksToWait).WaitAsync(timeoutValue).ConfigureAwait(false);
        }

        CleanupResources();
    }

    private void CleanupResources()
    {
        // 清理发送锁资源
        foreach (var semaphore in _endpointSendLocks.Values)
        {
            try
            {
                semaphore.Dispose();
            }
            catch
            { /* 忽略处置异常 */
            }
        }
        _endpointSendLocks.Clear();

        // 清理核心资源
        _socket?.Dispose();
        _socket = null;
        _cts?.Dispose();
        _cts = null;
        _receiveTask = null;
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        try
        {
            StopAsync().GetAwaiter().GetResult();
        }
        finally
        {
            _concurrencyLimiter.Dispose();
            _disposed = true;
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;

        try
        {
            await StopAsync().ConfigureAwait(false);
        }
        finally
        {
            _concurrencyLimiter.Dispose();
            _disposed = true;
        }
    }
}
