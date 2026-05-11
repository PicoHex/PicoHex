namespace PicoCfg;

internal sealed class FileWatchingCfgProvider : ICfgProvider
{
    private readonly ICfgProvider _inner;
    private readonly string _filePath;
    private readonly TimeSpan _debounceInterval;
    private FileSystemWatcher? _watcher;
    private CancellationTokenSource? _debounceCts;
    private readonly Lock _debounceLock = new();
    private int _disposed;

    internal FileWatchingCfgProvider(ICfgProvider inner, string filePath, TimeSpan? debounceInterval = null)
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

        _watcher?.Dispose();
        _debounceCts?.Cancel();
        _debounceCts?.Dispose();
        await _inner.DisposeAsync();
    }

    private void StartWatcher()
    {
        var dir = Path.GetDirectoryName(_filePath) ?? ".";
        var file = Path.GetFileName(_filePath);
        _watcher = new FileSystemWatcher(dir, file)
        {
            EnableRaisingEvents = true
        };
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
            _debounceCts = new CancellationTokenSource();
            var capturedCts = _debounceCts;
            Task.Delay(_debounceInterval, capturedCts.Token)
                .ContinueWith(
                    async _ =>
                    {
                        try
                        {
                            await _inner.ReloadAsync(CancellationToken.None);
                        }
                        catch
                        {
                            /* swallow: don't crash watcher */
                        }
                    },
                    CancellationToken.None,
                    TaskContinuationOptions.NotOnCanceled,
                    TaskScheduler.Default
                );
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
        catch
        {
            /* best-effort cleanup */
        }

        if (Volatile.Read(ref _disposed) == 1)
            return;

        StartWatcher();
    }
}
