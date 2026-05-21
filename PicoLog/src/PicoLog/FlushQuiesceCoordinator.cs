namespace PicoLog;

internal sealed class FlushQuiesceCoordinator
{
    private readonly Lock _stateLock = new();
    private int _flushPending;
    private int _activeWriteOperations;
    private TaskCompletionSource _stateChanged = CreateSignal();

    public void EnterWriteOperationSync(TimeSpan timeout)
    {
        var startTimestamp = Stopwatch.GetTimestamp();
        var spinWait = new SpinWait();

        while (true)
        {
            Task waitTask;

            lock (_stateLock)
            {
                if (_flushPending == 0)
                {
                    _activeWriteOperations++;
                    return;
                }

                waitTask = _stateChanged.Task;
            }

            var remaining = timeout - Stopwatch.GetElapsedTime(startTimestamp);

            if (remaining <= TimeSpan.Zero)
                throw new TimeoutException(
                    "Timed out waiting to enter a write operation while a flush is in progress."
                );

            if (!waitTask.Wait(remaining))
                throw new TimeoutException(
                    "Timed out waiting to enter a write operation while a flush is in progress."
                );

            spinWait.SpinOnce();
        }
    }

    public async ValueTask EnterWriteOperationAsync(CancellationToken cancellationToken)
    {
        while (true)
        {
            Task waitTask;

            lock (_stateLock)
            {
                if (_flushPending == 0)
                {
                    _activeWriteOperations++;
                    return;
                }

                waitTask = _stateChanged.Task;
            }

            await waitTask.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    public void ExitWriteOperation()
    {
        lock (_stateLock)
        {
            _activeWriteOperations--;
            SignalStateChangedUnderLock();
        }
    }

    public async ValueTask BlockWritesAsync(CancellationToken cancellationToken)
    {
        Task waitTask;

        while (true)
        {
            lock (_stateLock)
            {
                _flushPending = 1;

                if (_activeWriteOperations == 0)
                    return;

                waitTask = _stateChanged.Task;
            }

            await waitTask.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    public async ValueTask WaitForIdleAsync(
        Func<bool> isOwnerIdleUnderLock,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(isOwnerIdleUnderLock);

        Task waitTask;

        while (true)
        {
            lock (_stateLock)
            {
                if (CanStopWaitingForIdleUnderLock(isOwnerIdleUnderLock))
                    return;

                waitTask = _stateChanged.Task;
            }

            await waitTask.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    public void ResumeWrites()
    {
        lock (_stateLock)
        {
            _flushPending = 0;
            SignalStateChangedUnderLock();
        }
    }

    public bool IsFlushPending()
    {
        lock (_stateLock)
            return _flushPending != 0;
    }

    internal bool HasActiveWriteOperations()
    {
        lock (_stateLock)
            return _activeWriteOperations != 0;
    }

    public void BeginOwnerActivity(Action beginOwnerActivityUnderLock)
    {
        ArgumentNullException.ThrowIfNull(beginOwnerActivityUnderLock);

        lock (_stateLock)
            beginOwnerActivityUnderLock();
    }

    public void EndOwnerActivity(Action endOwnerActivityUnderLock, Func<bool> isOwnerIdleUnderLock)
    {
        ArgumentNullException.ThrowIfNull(endOwnerActivityUnderLock);
        ArgumentNullException.ThrowIfNull(isOwnerIdleUnderLock);

        lock (_stateLock)
        {
            endOwnerActivityUnderLock();
            SignalStateChangedUnderLock();
        }
    }

    private bool CanStopWaitingForIdleUnderLock(Func<bool> isOwnerIdleUnderLock) =>
        _flushPending == 0 || (_activeWriteOperations == 0 && isOwnerIdleUnderLock());

    private void SignalStateChangedUnderLock()
    {
        var signal = _stateChanged;
        _stateChanged = CreateSignal();
        signal.TrySetResult();
    }

    private static TaskCompletionSource CreateSignal() =>
        new(TaskCreationOptions.RunContinuationsAsynchronously);
}
