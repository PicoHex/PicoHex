namespace Pico.Node
{
    public sealed class UdpNode : INode
    {
        // Configuration
        private readonly IPAddress _ipAddress;
        private readonly ushort _port;
        private readonly ILogger<UdpNode> _logger;
        private readonly Func<IUdpHandler> _udpHandlerFactory;
        private readonly Action<Exception, string>? _exceptionHandler;
        private readonly Action<UdpClient>? _configureUdpClient;
        private readonly SemaphoreSlim _concurrencyLimiter;

        // Runtime state
        private readonly ConcurrentDictionary<Task, bool> _activeTasks = new();
        private volatile bool _isDisposed;
        private readonly CancellationTokenSource _serverCts = new();
        private readonly Lock _stateLock = new();
        private bool _isRunning;
        private UdpClient? _udpClient;
        private Task? _serverTask;

        public UdpNode(
            IPAddress ipAddress,
            ushort port,
            Func<IUdpHandler> udpHandlerFactory,
            ILogger<UdpNode> logger,
            Action<Exception, string>? exceptionHandler = null,
            int maxConcurrency = 1000,
            Action<UdpClient>? configureUdpClient = null
        )
        {
            ArgumentNullException.ThrowIfNull(ipAddress);
            ArgumentNullException.ThrowIfNull(udpHandlerFactory);
            ArgumentNullException.ThrowIfNull(logger);

            _ipAddress = ipAddress;
            _port = port;
            _udpHandlerFactory = udpHandlerFactory;
            _logger = logger;
            _exceptionHandler = exceptionHandler;
            _configureUdpClient = configureUdpClient;
            _concurrencyLimiter = new SemaphoreSlim(maxConcurrency);
        }

        public Task StartAsync(CancellationToken cancellationToken = default)
        {
            lock (_stateLock)
            {
                if (_isRunning)
                    return Task.CompletedTask;

                try
                {
                    // Initialize UDP client
                    _udpClient = new UdpClient(new IPEndPoint(_ipAddress, _port));
                    _configureUdpClient?.Invoke(_udpClient);
                    _isRunning = true;
                }
                catch (Exception ex)
                {
                    _logger.CriticalAsync(
                        "Failed to initialize UDP server",
                        ex,
                        cancellationToken: cancellationToken
                    );
                    _udpClient?.Dispose();
                    throw;
                }

                // Start server in background task
                _serverTask = RunServerAsync(cancellationToken);
            }

            return Task.CompletedTask;
        }

        private async Task RunServerAsync(CancellationToken externalToken)
        {
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
                externalToken,
                _serverCts.Token
            );

            var serverToken = linkedCts.Token;

            await SafeLogAsync(
                async () =>
                    await _logger.InfoAsync(
                        $"UDP server started on {_ipAddress}:{_port}",
                        cancellationToken: serverToken
                    ),
                serverToken
            );

            try
            {
                while (!serverToken.IsCancellationRequested)
                {
                    try
                    {
                        // Receive datagram without blocking on concurrency limiter
                        var receiveResult = await _udpClient!
                            .ReceiveAsync(serverToken)
                            .ConfigureAwait(false);

                        // Create processing task
                        var processTask = Task.Run(
                            async () =>
                            {
                                try
                                {
                                    // Acquire concurrency slot
                                    await _concurrencyLimiter.WaitAsync(serverToken);
                                    await ProcessDatagramAsync(
                                        receiveResult.Buffer,
                                        receiveResult.RemoteEndPoint,
                                        serverToken
                                    );
                                }
                                finally
                                {
                                    // Ensure slot is always released
                                    _concurrencyLimiter.Release();
                                }
                            },
                            serverToken
                        );

                        // Track task and set up cleanup
                        _activeTasks.TryAdd(processTask, true);
                        await processTask.ContinueWith(
                            t => _activeTasks.TryRemove(t, out _),
                            TaskContinuationOptions.ExecuteSynchronously
                        );
                    }
                    catch (OperationCanceledException) when (serverToken.IsCancellationRequested)
                    {
                        // Normal shutdown
                    }
                    catch (SocketException ex)
                    {
                        await HandleSocketExceptionAsync(ex, serverToken);
                    }
                    catch (ObjectDisposedException)
                    {
                        // Server is shutting down
                    }
                    catch (Exception ex)
                    {
                        await SafeLogAsync(
                            async () =>
                                await _logger.ErrorAsync(
                                    "UDP server receive error",
                                    ex,
                                    cancellationToken: serverToken
                                ),
                            serverToken
                        );
                    }
                }
            }
            finally
            {
                StopServer();
                lock (_stateLock)
                    _isRunning = false;

                await SafeLogAsync(
                    async () =>
                        await _logger.InfoAsync(
                            "UDP server stopped",
                            cancellationToken: serverToken
                        ),
                    CancellationToken.None
                );
            }
        }

        private async Task ProcessDatagramAsync(
            byte[] datagram,
            IPEndPoint remoteEndpoint,
            CancellationToken cancellationToken
        )
        {
            try
            {
                var handler = _udpHandlerFactory();
                await handler
                    .HandleAsync(datagram, remoteEndpoint, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // Normal cancellation
            }
            catch (Exception ex)
            {
                await SafeLogAsync(
                    async () =>
                        await _logger.ErrorAsync(
                            $"Error processing UDP datagram from {remoteEndpoint}",
                            ex,
                            cancellationToken: cancellationToken
                        ),
                    cancellationToken
                );

                _exceptionHandler?.Invoke(ex, remoteEndpoint.ToString());
            }
        }

        private async Task HandleSocketExceptionAsync(SocketException ex, CancellationToken token)
        {
            if (IsCriticalSocketError(ex.SocketErrorCode))
            {
                await SafeLogAsync(
                    async () =>
                        await _logger.CriticalAsync(
                            $"Critical UDP socket error: {ex.SocketErrorCode}",
                            ex,
                            cancellationToken: token
                        ),
                    token
                );
                StopServer();
            }
            else
            {
                await SafeLogAsync(
                    async () =>
                        await _logger.WarningAsync(
                            $"Non-critical UDP socket error: {ex.SocketErrorCode}",
                            ex,
                            cancellationToken: token
                        ),
                    token
                );
            }
        }

        public async Task StopAsync(CancellationToken cancellationToken = default)
        {
            StopServer();

            if (!_activeTasks.IsEmpty)
            {
                try
                {
                    // Wait for active tasks with timeout
                    var completionTask = Task.WhenAll(_activeTasks.Keys);
                    var timeout = TimeSpan.FromSeconds(5);
                    var timeoutTask = Task.Delay(timeout, cancellationToken);

                    await Task.WhenAny(completionTask, timeoutTask).ConfigureAwait(false);

                    if (!completionTask.IsCompleted)
                    {
                        await SafeLogAsync(
                            async () =>
                                await _logger.WarningAsync(
                                    "Timeout waiting for tasks during shutdown",
                                    cancellationToken: cancellationToken
                                ),
                            CancellationToken.None
                        );
                    }
                }
                catch (OperationCanceledException)
                {
                    // External cancellation requested
                }
            }
        }

        private void StopServer()
        {
            lock (_stateLock)
            {
                if (!_isRunning)
                    return;

                // Signal shutdown
                _serverCts.Cancel();

                // Close UDP client
                try
                {
                    _udpClient?.Close();
                }
                catch (Exception ex)
                {
                    _logger.WarningAsync("Error closing UDP client", ex);
                }
                finally
                {
                    _udpClient = null;
                }
            }
        }

        private static bool IsCriticalSocketError(SocketError error) =>
            error switch
            {
                SocketError.AccessDenied => true,
                SocketError.AddressAlreadyInUse => true,
                SocketError.AddressNotAvailable => true,
                SocketError.InvalidArgument => true,
                SocketError.Shutdown => true,
                SocketError.ConnectionReset => true,
                _ => false
            };

        public async ValueTask DisposeAsync()
        {
            if (_isDisposed)
                return;

            try
            {
                StopServer();

                // Wait for server task to complete
                if (_serverTask != null)
                {
                    try
                    {
                        await _serverTask.WaitAsync(TimeSpan.FromSeconds(3)).ConfigureAwait(false);
                    }
                    catch (TimeoutException)
                    {
                        await _logger.WarningAsync(
                            "Timeout waiting for server task during disposal"
                        );
                    }
                }

                // Wait for active processing tasks
                if (!_activeTasks.IsEmpty)
                {
                    try
                    {
                        var timeout = TimeSpan.FromSeconds(5);
                        await Task.WhenAll(_activeTasks.Keys)
                            .WaitAsync(timeout)
                            .ConfigureAwait(false);
                    }
                    catch (TimeoutException)
                    {
                        await _logger.WarningAsync(
                            "Timeout waiting for processing tasks during disposal"
                        );
                    }
                }
            }
            finally
            {
                lock (_stateLock)
                {
                    if (!_isDisposed)
                    {
                        _udpClient?.Dispose();
                        _serverCts.Dispose();
                        _concurrencyLimiter.Dispose();
                        _isDisposed = true;
                    }
                }
            }
        }

        private async Task SafeLogAsync(Func<Task> logAction, CancellationToken cancellationToken)
        {
            try
            {
                // Skip logging if cancellation was requested
                if (cancellationToken.IsCancellationRequested)
                    return;

                await logAction().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                // Fallback to synchronous logging if async fails
                try
                {
                    await _logger.WarningAsync(
                        "Failed to write log message",
                        ex,
                        cancellationToken: cancellationToken
                    );
                }
                catch
                {
                    // Final fallback - ignore
                }
            }
        }
    }
}
