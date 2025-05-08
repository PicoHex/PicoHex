namespace Pico.Cfg;

public class StreamChangeToken : IAsyncChangeToken
{
    private readonly Lock _syncLock = new();
    private CancellationTokenSource _cts = new();

    public bool HasChanged { get; private set; }

    public ValueTask WaitForChangeAsync(CancellationToken ct = default)
    {
        var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, _cts.Token);
        return new ValueTask(linkedCts.Token.WaitForCancellationAsync());
    }

    public void NotifyChanged()
    {
        lock (_syncLock)
        {
            if (_cts.IsCancellationRequested)
            {
                var oldCts = _cts;
                _cts = new CancellationTokenSource();
                oldCts.Dispose();
            }
            _cts.Cancel();
            HasChanged = true;
        }
    }

    public void Reset()
    {
        lock (_syncLock)
        {
            HasChanged = false;
        }
    }
}

public static class CancellationTokenExtensions
{
    public static Task WaitForCancellationAsync(this CancellationToken ct)
    {
        var tcs = new TaskCompletionSource<bool>();
        ct.Register(() => tcs.TrySetResult(true));
        return tcs.Task;
    }
}
