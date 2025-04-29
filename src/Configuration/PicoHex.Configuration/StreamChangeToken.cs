namespace PicoHex.Configuration;

public class StreamChangeToken : IAsyncChangeToken
{
    private TaskCompletionSource<bool> _tcs = new();

    public bool HasChanged => _tcs.Task.IsCompleted;

    public ValueTask WaitForChangeAsync(CancellationToken ct = default) =>
        new ValueTask(_tcs.Task.WaitAsync(ct));

    public void NotifyChanged()
    {
        var oldTcs = _tcs;
        _tcs = new TaskCompletionSource<bool>();
        oldTcs.TrySetResult(true);
    }
}
