namespace PicoCfg;

internal sealed class FileWatchingCfgProvider : ICfgProvider
{
    private readonly ICfgProvider _inner;
    private readonly string _filePath;
    private readonly TimeSpan _debounceInterval;
    private FileSystemWatcher? _watcher;
    private CancellationTokenSource? _debounceCts;
    private Task? _pendingReload;
    private readonly Lock _debounceLock = new();
    private int _disposed;

    /// <summary>
    /// Optional callback for observing errors during reload/cleanup.
    /// Receives context string ("reload" or "cleanup") and the caught exception.
    /// Expected exceptions include <see cref="IOException"/> (file locked/deleted),
    /// <see cref="ObjectDisposedException"/> (provider already disposed), etc.
    /// </summary>
    internal Action<string, Exception>? OnError;

    internal FileWatchingCfgProvider(
        ICfgProvider inner,
        string filePath,
        TimeSpan? debounceInterval = null
    )
    {
        ArgumentNullException.ThrowIfNull(inner);
        ArgumentNullException.ThrowIfNull(filePath);
        _inner = inner;
        _filePath = filePath;
        _debounceInterval = debounceInterval ?? TimeSpan.FromMilliseconds(200);
        StartWatcher();
    }

    public ICfgSnapshot Snapshot => _inner.Snapshot;

    public ValueTask<bool> ReloadAsync(CancellationToken ct = default) => _inner.ReloadAsync(ct);

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
            return;

        try
        {
            _watcher?.Dispose();
        }
        catch (Exception ex)
        {
            OnError?.Invoke("dispose", ex);
            Trace.TraceError($"[PicoCfg] File watching dispose error: {ex}");
        }

        Task? pendingReload;
        lock (_debounceLock)
        {
            _debounceCts?.Cancel();
            _debounceCts?.Dispose();
            _debounceCts = null;
            pendingReload = _pendingReload;
        }

        if (pendingReload is not null)
        {
            try
            {
                await pendingReload.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            { /* reload cancelled during shutdown */
            }
            catch (Exception ex)
            {
                OnError?.Invoke("reload", ex);
                Trace.TraceError($"[PicoCfg] File watching reload error during dispose: {ex}");
            }
        }

        await _inner.DisposeAsync();
    }

    private void StartWatcher()
    {
        var dir = Path.GetDirectoryName(_filePath) ?? ".";
        var file = Path.GetFileName(_filePath);
        _watcher = new FileSystemWatcher(dir, file) { EnableRaisingEvents = true };
        _watcher.Changed += OnFileChanged;
        _watcher.Created += OnFileChanged;
        _watcher.Error += OnWatcherError;
    }

    private void OnFileChanged(object sender, FileSystemEventArgs e)
    {
        if (Volatile.Read(ref _disposed) == 1)
            return;

        lock (_debounceLock)
        {
            if (Volatile.Read(ref _disposed) == 1)
                return;

            _debounceCts?.Cancel();
            _debounceCts?.Dispose();
            _debounceCts = new CancellationTokenSource();
            var capturedCt = _debounceCts.Token;

            _pendingReload = DebounceAndReloadAsync(capturedCt);
        }
    }

    private async Task DebounceAndReloadAsync(CancellationToken ct)
    {
        try
        {
            await Task.Delay(_debounceInterval, ct).ConfigureAwait(false);
            await _inner.ReloadAsync(ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
        catch (Exception ex)
        {
            OnError?.Invoke("reload", ex);
            Trace.TraceError($"[PicoCfg] File watching reload error: {ex}");
        }
    }

    private void OnWatcherError(object sender, ErrorEventArgs e)
    {
        if (Volatile.Read(ref _disposed) == 1)
            return;

        try
        {
            _watcher?.Dispose();
        }
        catch (Exception ex)
        {
            OnError?.Invoke("cleanup", ex);
            Trace.TraceError($"[PicoCfg] File watching cleanup error: {ex}");
        }

        if (Volatile.Read(ref _disposed) == 1)
            return;

        StartWatcher();
    }
}
