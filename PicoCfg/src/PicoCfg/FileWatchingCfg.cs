namespace PicoCfg;

internal sealed class FileWatchingCfgSource : ICfgSource
{
    private readonly ICfgSource _innerSource;
    private readonly string _filePath;
    private readonly TimeSpan? _debounceInterval;
    private readonly Action<string, Exception>? _onError;

    internal FileWatchingCfgSource(
        ICfgSource innerSource,
        string filePath,
        TimeSpan? debounceInterval,
        Action<string, Exception>? onError = null
    )
    {
        ArgumentNullException.ThrowIfNull(innerSource);
        ArgumentNullException.ThrowIfNull(filePath);
        _innerSource = innerSource;
        _filePath = filePath;
        _debounceInterval = debounceInterval;
        _onError = onError;
    }

    public async ValueTask<ICfgProvider> OpenAsync(CancellationToken ct = default)
    {
        var inner = await _innerSource.OpenAsync(ct);
        return new FileWatchingCfgProvider(inner, _filePath, _debounceInterval)
        {
            OnError = _onError,
        };
    }
}

internal sealed class FileWatchingCfgProvider : ICfgProvider
{
    private readonly ICfgProvider _inner;
    private readonly string _filePath;
    private readonly TimeSpan _debounceInterval;
    private FileSystemWatcher? _watcher;
    private CancellationTokenSource? _debounceCts;
    private Task? _pendingReload;
    private readonly Lock _debounceLock = new();
    private readonly Lock _watcherLock = new();
    private System.Threading.Timer? _watchRetryTimer;
    private int _disposed;

    /// <summary>Interval between attempts to re-create the watcher after its directory disappeared.</summary>
    private const int WatchRetryIntervalMs = 2000;

    /// <summary>
    /// Optional callback for observing errors during reload/cleanup.
    /// Receives context string ("reload" or "cleanup") and the caught exception.
    /// Expected exceptions include <see cref="IOException"/> (file locked/deleted),
    /// <see cref="ObjectDisposedException"/> (provider already disposed), etc.
    /// </summary>
    public Action<string, Exception>? OnError;

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
        TryStartWatcher();
    }

    public ICfgSnapshot Snapshot => _inner.Snapshot;

    public ValueTask<bool> ReloadAsync(CancellationToken ct = default) => _inner.ReloadAsync(ct);

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
            return;

        try
        {
            lock (_watcherLock)
            {
                _watchRetryTimer?.Dispose();
                _watchRetryTimer = null;
                _watcher?.Dispose();
                _watcher = null;
            }
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

    /// <summary>
    /// Creates the <see cref="FileSystemWatcher"/> for the watched file.
    /// Tolerates a missing watched directory: the failure is surfaced through
    /// <see cref="OnError"/> (context "watch") and a retry timer re-attempts
    /// until the directory exists again.
    /// </summary>
    private void TryStartWatcher()
    {
        lock (_watcherLock)
        {
            if (Volatile.Read(ref _disposed) == 1)
                return;

            var dir = Path.GetDirectoryName(_filePath) ?? ".";
            var file = Path.GetFileName(_filePath);
            try
            {
                var watcher = new FileSystemWatcher(dir, file) { EnableRaisingEvents = true };
                watcher.Changed += OnFileChanged;
                watcher.Created += OnFileChanged;
                watcher.Error += OnWatcherError;
                _watcher = watcher;

                _watchRetryTimer?.Dispose();
                _watchRetryTimer = null;
            }
            catch (Exception ex) when (IsWatchStartException(ex))
            {
                OnError?.Invoke("watch", ex);
                Trace.TraceError(
                    $"[PicoCfg] File watching could not start for '{_filePath}': {ex}"
                );

                // The watched directory does not exist (yet) — retry until it appears.
                _watchRetryTimer ??= new Timer(
                    static state => ((FileWatchingCfgProvider)state!).TryStartWatcher(),
                    this,
                    WatchRetryIntervalMs,
                    WatchRetryIntervalMs
                );
            }
        }
    }

    private static bool IsWatchStartException(Exception ex) =>
        ex
            is ArgumentException
                or IOException
                or UnauthorizedAccessException
                or NotSupportedException;

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

        FileSystemWatcher? failedWatcher;
        lock (_watcherLock)
        {
            failedWatcher = _watcher;
            _watcher = null;
        }

        try
        {
            failedWatcher?.Dispose();
        }
        catch (Exception ex)
        {
            OnError?.Invoke("cleanup", ex);
            Trace.TraceError($"[PicoCfg] File watching cleanup error: {ex}");
        }

        if (Volatile.Read(ref _disposed) == 1)
            return;

        TryStartWatcher();
    }
}
