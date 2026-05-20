namespace PicoLog;

internal sealed class CategoryPipeline : IDisposable, IAsyncDisposable
{
    private readonly string _categoryName;
    private readonly LoggerFactoryRuntime _runtime;
    private readonly InternalLogSinkDispatcher _sinkDispatcher;
    private readonly InternalLoggerQueue _queue;
    private readonly Task _processingTask;
    private readonly int _queueDepthProviderId;
    private readonly SemaphoreSlim _flushSemaphore = new(1, 1);
    private readonly FlushQuiesceCoordinator _flushQuiesceCoordinator = new();
    private int _disposeState;
    private int _activeDequeuedEntries;
    private int _activeDispatchOperations;
    private long _droppedEntries;

    public CategoryPipeline(string categoryName, LoggerFactoryRuntime runtime)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(categoryName);

        _categoryName = categoryName;
        _runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
        _sinkDispatcher = new InternalLogSinkDispatcher(_runtime);
        _queue = new InternalLoggerQueue(_runtime);
        _processingTask = ProcessEntriesAsync();
        _queueDepthProviderId = PicoLogMetrics.RegisterQueueDepthProvider(
            _queue.GetQueuedEntryCount
        );
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
    /// Synchronously disposes the pipeline. Uses synchronous semaphore wait
    /// to avoid sync-over-async deadlock risks, then delegates remaining
    /// asynchronous work to <see cref="DisposeAsyncCore"/>.
    /// Prefer calling <see cref="DisposeAsync"/> directly.
    /// </summary>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposeState, 1) != 0)
            return;

        _flushSemaphore.Wait();

        try
        {
            DisposeAsyncCore().AsTask().GetAwaiter().GetResult();
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
        // (line 80) or blocks on the semaphore acquisition (line 82).
        await _flushSemaphore.WaitAsync().ConfigureAwait(false);

        try
        {
            await DisposeAsyncCore().ConfigureAwait(false);
        }
        finally
        {
            _flushSemaphore.Dispose();
        }
    }

    private async ValueTask DisposeAsyncCore()
    {
        Exception? processingException = null;
        var shutdownTimeout = _runtime.ShutdownTimeout;

        try
        {
            using var drainCts =
                shutdownTimeout > TimeSpan.Zero
                    ? new CancellationTokenSource(shutdownTimeout)
                    : null;

            if (drainCts is not null)
                _sinkDispatcher.BeginDrain(drainCts.Token);

            _queue.Complete();

            if (drainCts is not null)
            {
                try
                {
                    await _processingTask.WaitAsync(drainCts.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    // Drain timeout: sinks have been notified via the drain token.
                    // Fall through to await the processing task without a timeout
                    // so it finishes draining before we dispose resources.
                }
            }

            await _processingTask.ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            processingException = ex;
        }
        finally
        {
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

    private async Task ProcessEntriesAsync()
    {
        while (await _queue.WaitToReadAsync().ConfigureAwait(false))
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
                        await _sinkDispatcher.DispatchEntryAsync(entry).ConfigureAwait(false);
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
