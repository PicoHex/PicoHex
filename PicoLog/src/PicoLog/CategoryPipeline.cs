namespace PicoLog;

internal sealed class CategoryPipeline : IDisposable, IAsyncDisposable
{
    private readonly string _categoryName;
    private readonly LoggerFactoryRuntime _runtime;
    private readonly InternalLogSinkDispatcher _sinkDispatcher;
    private readonly InternalLoggerQueue _queue;
    private readonly Thread _processingThread;
    private readonly TaskCompletionSource _processingExited =
        new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly int _queueDepthProviderId;
    private readonly SemaphoreSlim _flushSemaphore = new(1, 1);
    private readonly FlushQuiesceCoordinator _flushQuiesceCoordinator = new();
    private int _disposeState;
    private int _activeDequeuedEntries;
    private int _activeDispatchOperations;
    private long _droppedEntries;
    private Exception? _processingException;

    public CategoryPipeline(string categoryName, LoggerFactoryRuntime runtime)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(categoryName);

        _categoryName = categoryName;
        _runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
        _sinkDispatcher = new InternalLogSinkDispatcher(_runtime);
        _queue = new InternalLoggerQueue(_runtime);
        // Dedicated background thread instead of a ThreadPool task. The processing
        // loop performs sync-over-async on this thread to bridge async sink APIs;
        // because the thread is not a pool worker, blocking it here cannot starve
        // the ThreadPool and therefore cannot deadlock with sync Dispose paths
        // (this matters most on resource-constrained linux-arm64 CI VMs).
        _processingThread = new Thread(ProcessEntries)
        {
            IsBackground = true,
            Name = $"PicoLog.Pipeline[{categoryName}]"
        };
        _queueDepthProviderId = PicoLogMetrics.RegisterQueueDepthProvider(
            _queue.GetQueuedEntryCount
        );
        _processingThread.Start();
    }

    public void Write(LogEntry entry)
    {
        try
        {
            EnterWriteOperationSync();
        }
        catch (TimeoutException) when (_runtime.DropMessagesOnFlush)
        {
            // Flush is in progress and the sync write timed out.
            // DropMessagesOnFlush is enabled — drop this entry rather than
            // blocking the calling thread indefinitely.
            ReportDroppedMessage();
            return;
        }

        try
        {
            HandleWriteResult(_queue.TryEnqueueSync(entry));
        }
        finally
        {
            ExitWriteOperation();
        }
    }

    public async Task WriteAsync(LogEntry entry, CancellationToken cancellationToken)
    {
        await EnterWriteOperationAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            var writeTask = _queue.TryEnqueueAsync(entry, cancellationToken);
            if (writeTask.IsCompletedSuccessfully)
            {
                HandleWriteResult(writeTask.Result);
                return;
            }

            HandleWriteResult(await writeTask.ConfigureAwait(false));
        }
        finally
        {
            ExitWriteOperation();
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
        }
        finally
        {
            ResumeWrites();
            _flushSemaphore.Release();
        }
    }

    /// <summary>
    /// Synchronously disposes the pipeline. Fully synchronous — no sync-over-async
    /// bridging — so callers can safely invoke this from any thread, including
    /// ThreadPool workers, without risking ThreadPool starvation deadlocks.
    /// Shutdown blocks on <see cref="Thread.Join()"/> of the dedicated processing
    /// thread; because that thread is not a pool worker, the pool remains free
    /// to service async continuations the dispatcher relies on.
    /// </summary>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposeState, 1) != 0)
            return;

        _flushSemaphore.Wait();

        try
        {
            ShutdownCore();
        }
        finally
        {
            _flushSemaphore.Dispose();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposeState, 1) != 0)
            return;

        // Acquire the flush semaphore BEFORE completing the queue to prevent
        // a race with FlushAsync. Once we hold the semaphore, no concurrent
        // FlushAsync can proceed — it either detects _disposeState != 0
        // or blocks on the semaphore acquisition.
        await _flushSemaphore.WaitAsync().ConfigureAwait(false);

        CancellationTokenSource? drainCts = null;

        try
        {
            // Begin drain and complete the queue inline (no pool worker
            // pinned). The dedicated processing thread observes completion
            // (channel completion uses AllowSynchronousContinuations = true
            // so the wakeup is inline), drains, and signals _processingExited
            // from its own thread.
            var shutdownTimeout = _runtime.ShutdownTimeout;
            drainCts =
                shutdownTimeout > TimeSpan.Zero
                    ? new CancellationTokenSource(shutdownTimeout)
                    : null;

            if (drainCts is not null)
                _sinkDispatcher.BeginDrain(drainCts.Token);

            _queue.Complete();

            // Await the TCS — zero pool workers are pinned during this wait.
            await _processingExited.Task.ConfigureAwait(false);

            PicoLogMetrics.UnregisterQueueDepthProvider(_queueDepthProviderId);

            if (_processingException is { } ex)
                ExceptionDispatchInfo.Throw(ex);
        }
        finally
        {
            _sinkDispatcher.Dispose();
            drainCts?.Dispose();
            _flushSemaphore.Dispose();
        }
    }

    private void ShutdownCore()
    {
        Exception? processingException = null;
        var shutdownTimeout = _runtime.ShutdownTimeout;
        CancellationTokenSource? drainCts = null;

        try
        {
            if (shutdownTimeout > TimeSpan.Zero)
            {
                drainCts = new CancellationTokenSource(shutdownTimeout);
                _sinkDispatcher.BeginDrain(drainCts.Token);
            }

            _queue.Complete();

            // Wait for processing thread to finish draining. If the drain CTS
            // fires, sinks observe cancellation via the drain token and abort,
            // which causes the loop to exit promptly; Join then returns.
            _processingThread.Join();
            processingException = _processingException;
        }
        catch (Exception ex)
        {
            processingException = ex;
        }
        finally
        {
            _sinkDispatcher.Dispose();
            drainCts?.Dispose();
            PicoLogMetrics.UnregisterQueueDepthProvider(_queueDepthProviderId);
        }

        if (processingException is not null)
            ExceptionDispatchInfo.Throw(processingException);
    }

    private void HandleWriteResult(LogWriteResult result)
    {
        switch (result)
        {
            case LogWriteResult.AcceptedAfterEviction
            or LogWriteResult.DroppedNewWrite:
                ReportDroppedMessage();
                break;
            case LogWriteResult.RejectedAfterShutdown:
                _runtime.RecordRejectedAfterShutdown();
                break;
        }
    }

    private void ReportDroppedMessage()
    {
        var dropped = Interlocked.Increment(ref _droppedEntries);
        _runtime.ReportDroppedMessages(_categoryName, dropped);
    }

    private void ProcessEntries()
    {
        try
        {
            while (WaitForRead())
            {
                while (true)
                {
                    BeginDequeuedEntry();

                    if (!_queue.TryRead(out var entry))
                    {
                        EndDequeuedEntry();
                        break;
                    }

                    try
                    {
                        BeginDispatch();

                        try
                        {
                            // Sync-over-async on this dedicated thread is intentional and safe:
                            // continuations of DispatchEntryAsync run on the ThreadPool, which
                            // is never blocked by this thread (it is not a pool worker).
                            _sinkDispatcher.DispatchEntryAsync(entry).GetAwaiter().GetResult();
                        }
                        finally
                        {
                            EndDispatch();
                        }
                    }
                    finally
                    {
                        EndDequeuedEntry();
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
        var waitTask = _queue.WaitToReadAsync();
        return waitTask.IsCompletedSuccessfully
            ? waitTask.Result
            : waitTask.AsTask().GetAwaiter().GetResult();
    }

    private void EnterWriteOperationSync() =>
        _flushQuiesceCoordinator.EnterWriteOperationSync(_runtime.SyncWriteTimeout);

    private ValueTask EnterWriteOperationAsync(CancellationToken cancellationToken) =>
        _flushQuiesceCoordinator.EnterWriteOperationAsync(cancellationToken);

    private void ExitWriteOperation() => _flushQuiesceCoordinator.ExitWriteOperation();

    private ValueTask BlockWritesAsync(CancellationToken cancellationToken) =>
        _flushQuiesceCoordinator.BlockWritesAsync(cancellationToken);

    private ValueTask WaitForIdleAsync(CancellationToken cancellationToken) =>
        _flushQuiesceCoordinator.WaitForIdleAsync(IsOwnerIdleUnderLock, cancellationToken);

    private void ResumeWrites() => _flushQuiesceCoordinator.ResumeWrites();

    private void BeginDispatch() =>
        _flushQuiesceCoordinator.BeginOwnerActivity(() => _activeDispatchOperations++);

    private void BeginDequeuedEntry() =>
        _flushQuiesceCoordinator.BeginOwnerActivity(() => _activeDequeuedEntries++);

    private void EndDequeuedEntry() =>
        _flushQuiesceCoordinator.EndOwnerActivity(
            () => _activeDequeuedEntries--,
            IsOwnerIdleUnderLock
        );

    private void EndDispatch() =>
        _flushQuiesceCoordinator.EndOwnerActivity(
            () => _activeDispatchOperations--,
            IsOwnerIdleUnderLock
        );

    private bool IsOwnerIdleUnderLock() =>
        _activeDequeuedEntries == 0
        && _activeDispatchOperations == 0
        && _queue.GetQueuedEntryCount() == 0;
}
