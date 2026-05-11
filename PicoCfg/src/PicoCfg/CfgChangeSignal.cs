namespace PicoCfg;

internal sealed class CfgChangeSignal
{
    private readonly Lock _syncRoot = new();
    private bool _hasChanged;
    private readonly CancellationTokenSource _cts = new();
    private readonly Task _signalTask;

    public CfgChangeSignal()
    {
        _signalTask = _cts.Token.AwaitCancellationAsync(throwOnCancellation: false);
    }

    public bool HasChanged => Volatile.Read(ref _hasChanged);

    public ValueTask WaitForChangeAsync(CancellationToken ct = default)
    {
        var signalTask = GetSignalTask();
        if (signalTask is null)
            return ValueTask.CompletedTask;

        if (!ct.CanBeCanceled)
            return new ValueTask(signalTask);

        return new ValueTask(signalTask.WaitAsync(ct));
    }

    internal void NotifyChanged()
    {
        var ctsToCancel = TryMarkChanged();
        if (ctsToCancel is null)
            return;

        ctsToCancel.Cancel();
        ctsToCancel.Dispose();
    }

    private Task? GetSignalTask()
    {
        lock (_syncRoot)
        {
            if (_hasChanged)
                return null;

            return _signalTask;
        }
    }

    private CancellationTokenSource? TryMarkChanged()
    {
        lock (_syncRoot)
        {
            if (_hasChanged)
                return null;

            _hasChanged = true;
            return _cts;
        }
    }
}

internal static class CancellationAwaitExtensions
{
    public static Task AwaitCancellationAsync(
        this CancellationToken ct,
        bool throwOnCancellation = true
    )
    {
        if (!ct.CanBeCanceled)
            return Task.Delay(Timeout.InfiniteTimeSpan, ct);

        if (ct.IsCancellationRequested)
            return throwOnCancellation ? Task.FromCanceled(ct) : Task.CompletedTask;

        var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var registration = ct.Register(
            static state =>
            {
                var (source, shouldThrow, token) = ((TaskCompletionSource, bool, CancellationToken))
                    state!;
                if (shouldThrow)
                    source.TrySetCanceled(token);
                else
                    source.TrySetResult();
            },
            (tcs, throwOnCancellation, ct)
        );

        _ = tcs.Task.ContinueWith(
            _ => registration.Dispose(),
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default
        );

        return tcs.Task;
    }
}
