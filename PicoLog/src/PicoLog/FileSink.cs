namespace PicoLog;

public sealed class FileSink : ILogSink, IFlushableLogSink
{
    private readonly Channel<string> _channel;
    private readonly ILogFormatter _formatter;
    private readonly FileSinkOptions _options;
    private readonly StreamWriter _writer;
    private readonly Thread _processingThread;
    private readonly TaskCompletionSource _processingExited =
        new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly SemaphoreSlim _flushSemaphore = new(1, 1);
    private readonly FlushQuiesceCoordinator _flushQuiesceCoordinator = new();
    private int _disposeState;
    private int _activeDequeuedMessages;
    private int _activeBatchOperations;
    private CancellationTokenSource? _batchDelayCancellationSource;
    private Exception? _processingException;

    public FileSink(ILogFormatter formatter, string filePath = FileSinkOptions.DefaultFilePath)
        : this(formatter, new FileSinkOptions { FilePath = filePath }) { }

    public FileSink(ILogFormatter formatter, FileSinkOptions options)
    {
        _formatter = formatter ?? throw new ArgumentNullException(nameof(formatter));
        _options = (
            options ?? throw new ArgumentNullException(nameof(options))
        ).CreateValidatedCopy();

        var fullPath = Path.GetFullPath(_options.FilePath);
        var directory =
            Path.GetDirectoryName(fullPath)
            ?? throw new ArgumentException(
                $"File path must not be a root directory: '{_options.FilePath}'",
                nameof(options)
            );

        if (!Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        _channel = Channel.CreateBounded<string>(
            new BoundedChannelOptions(_options.QueueCapacity)
            {
                FullMode = BoundedChannelFullMode.Wait,
                SingleReader = true,
                // Synchronous continuations are REQUIRED here. See
                // InternalLoggerQueue for the full rationale: the dedicated
                // ProcessWrites thread blocks on _channel.Reader.WaitToReadAsync
                // via sync-over-async; without inline continuations, every
                // write requires a ThreadPool worker to wake the reader,
                // which on low-core ARM64 CI runners (win-arm64,
                // osx-arm64) with 166 parallel TUnit tests starves and
                // hangs file-sink tests indefinitely.
                AllowSynchronousContinuations = true
            }
        );

        var fileStream = new FileStream(
            fullPath,
            FileMode.OpenOrCreate,
            FileAccess.Write,
            FileShare.Read,
            bufferSize: 4096,
            useAsync: true
        );

        try
        {
            fileStream.Seek(0, SeekOrigin.End);
            _writer = new StreamWriter(fileStream, Encoding.UTF8);
        }
        catch
        {
            fileStream.Dispose();
            throw;
        }
        // Dedicated background thread instead of a ThreadPool task. Blocking I/O
        // on this thread cannot starve the ThreadPool, so sync Dispose paths can
        // safely Join() without risking deadlocks under thread pool pressure
        // (notably on linux-arm64 CI VMs with limited vCPUs).
        _processingThread = new Thread(ProcessWrites)
        {
            IsBackground = true,
            Name = "PicoLog.FileSink"
        };
        _processingThread.Start();
    }

    public async Task WriteAsync(LogEntry entry, CancellationToken cancellationToken = default)
    {
        if (Volatile.Read(ref _disposeState) != 0)
        {
            PicoLogMetrics.RecordRejectedAfterShutdown();
            return;
        }

        var message = _formatter.Format(entry);

        try
        {
            await _channel.Writer.WriteAsync(message, cancellationToken).ConfigureAwait(false);
        }
        catch (ChannelClosedException)
        {
            // Channel closed means the sink is shutting down.
            // Writes arriving after channel completion are expected
            // and silently discarded — the entry was already in flight
            // when disposal began.
        }
    }

    public async ValueTask FlushAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposeState) != 0, this);

        await _flushSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposeState) != 0, this);

            await BlockWritesAsync(cancellationToken).ConfigureAwait(false);
            await WaitForIdleAsync(cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            await _writer.FlushAsync().ConfigureAwait(false);
        }
        finally
        {
            ResumeWrites();
            _flushSemaphore.Release();
        }
    }

    private void ProcessWrites()
    {
        try
        {
            var batch = new List<string>(_options.BatchSize);

            while (WaitForRead())
            {
                while (true)
                {
                    BeginDequeuedMessage();

                    if (!_channel.Reader.TryRead(out var message))
                    {
                        EndDequeuedMessage();
                        break;
                    }

                    batch.Add(message);

                    try
                    {
                        BeginBatch();

                        try
                        {
                            DrainBatch(batch);
                        }
                        finally
                        {
                            EndBatch();
                        }
                    }
                    finally
                    {
                        EndDequeuedMessage();
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _processingException = ex;
        }
        finally
        {
            // Signal completion to async waiters (DisposeAsync) without pinning
            // a pool worker. RunContinuationsAsynchronously prevents reentrant
            // execution of the awaiter on this dedicated thread.
            _processingExited.TrySetResult();
        }
    }

    private bool WaitForRead()
    {
        var waitTask = _channel.Reader.WaitToReadAsync();
        return waitTask.IsCompletedSuccessfully
            ? waitTask.Result
            : waitTask.AsTask().GetAwaiter().GetResult();
    }

    private void DrainBatch(List<string> batch)
    {
        if (_options.FlushInterval > TimeSpan.Zero)
        {
            while (batch.Count < _options.BatchSize)
            {
                if (IsFlushPending())
                    break;

                // Synchronous read — no timer, no CancellationTokenSource, no Task.WhenAny.
                // If a message is already available, add it and keep filling the batch.
                // If not, flush immediately. This avoids the ARM64 timer reliability issue
                // entirely while preserving batching under load (messages arrive faster than
                // the processing loop can drain).
                if (!_channel.Reader.TryRead(out var message))
                    break;

                batch.Add(message);
            }
        }
        else
        {
            while (batch.Count < _options.BatchSize && _channel.Reader.TryRead(out var message))
            {
                batch.Add(message);
            }
        }

        foreach (var message in batch)
            _writer.WriteLine(message);

        _writer.Flush();
        batch.Clear();
    }

    /// <summary>
    /// Synchronously disposes the sink. Fully synchronous — no sync-over-async
    /// bridging — so callers can safely invoke this from any thread, including
    /// ThreadPool workers, without risking ThreadPool starvation deadlocks.
    /// </summary>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposeState, 1) != 0)
            return;

        ShutdownCore();
    }

    public ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposeState, 1) != 0)
            return ValueTask.CompletedTask;

        // Complete the channel inline (no pool worker pinned). The dedicated
        // processing thread observes completion (channel uses
        // AllowSynchronousContinuations = true so the wakeup is inline),
        // drains, and signals _processingExited from its own thread.
        _channel.Writer.TryComplete();
        return new ValueTask(ShutdownAsync());
    }

    private async Task ShutdownAsync()
    {
        Exception? processingException = null;

        try
        {
            // Await the TCS — zero pool workers are pinned during this wait.
            await _processingExited.Task.ConfigureAwait(false);
            processingException = _processingException;
        }
        catch (Exception ex)
        {
            processingException = ex;
        }

        try
        {
            _writer.Flush();
        }
        catch (Exception ex) when (processingException is null)
        {
            processingException = ex;
        }
        finally
        {
            _writer.Dispose();
        }

        // Drain any concurrent FlushAsync waiters by acquiring then releasing
        // the semaphore (async — no pool worker pinned on the wait).
        await _flushSemaphore.WaitAsync().ConfigureAwait(false);
        try
        {
            _flushSemaphore.Release();
        }
        catch (ObjectDisposedException)
        {
            // Semaphore was already disposed by another path.
        }

        if (processingException is not null)
            ExceptionDispatchInfo.Throw(processingException);
    }

    private void ShutdownCore()
    {
        _channel.Writer.TryComplete();

        Exception? processingException = null;

        try
        {
            // Wait for the dedicated processing thread to drain and exit.
            // Because it is not a ThreadPool worker, this Join cannot starve
            // the pool, even when called from a pool thread.
            _processingThread.Join();
            processingException = _processingException;
        }
        catch (Exception ex)
        {
            processingException = ex;
        }

        try
        {
            _writer.Flush();
        }
        catch (Exception ex) when (processingException is null)
        {
            processingException = ex;
        }
        finally
        {
            _writer.Dispose();
        }

        // Drain any concurrent FlushAsync waiters by acquiring then releasing
        // the semaphore. We intentionally do not Dispose it: concurrent
        // FlushAsync calls may still hold a reference after the TOCTOU
        // dispose-state check; the semaphore holds no unmanaged resources
        // (WaitAsync only) and will be reclaimed by the GC.
        _flushSemaphore.Wait();
        try
        {
            _flushSemaphore.Release();
        }
        catch (ObjectDisposedException)
        {
            // Semaphore was already disposed by another path.
        }

        if (processingException is not null)
            ExceptionDispatchInfo.Throw(processingException);
    }

    private ValueTask BlockWritesAsync(CancellationToken cancellationToken) =>
        _flushQuiesceCoordinator.BlockWritesAsync(cancellationToken);

    private ValueTask WaitForIdleAsync(CancellationToken cancellationToken) =>
        _flushQuiesceCoordinator.WaitForIdleAsync(IsOwnerIdleUnderLock, cancellationToken);

    private void ResumeWrites() => _flushQuiesceCoordinator.ResumeWrites();

    private void BeginBatch() =>
        _flushQuiesceCoordinator.BeginOwnerActivity(() => _activeBatchOperations++);

    private void BeginDequeuedMessage() =>
        _flushQuiesceCoordinator.BeginOwnerActivity(() => _activeDequeuedMessages++);

    private void EndDequeuedMessage() =>
        _flushQuiesceCoordinator.EndOwnerActivity(
            () => _activeDequeuedMessages--,
            IsOwnerIdleUnderLock
        );

    private void EndBatch() =>
        _flushQuiesceCoordinator.EndOwnerActivity(
            () => _activeBatchOperations--,
            IsOwnerIdleUnderLock
        );

    private bool IsFlushPending() => _flushQuiesceCoordinator.IsFlushPending();

    private bool IsOwnerIdleUnderLock() =>
        _activeDequeuedMessages == 0 && _activeBatchOperations == 0 && _channel.Reader.Count == 0;
}
