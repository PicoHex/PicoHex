namespace PicoCfg;

internal sealed class FileWatchingCfgSource : ICfgSource
{
    private readonly ICfgSource _innerSource;
    private readonly string _filePath;
    private readonly TimeSpan? _debounceInterval;

    internal FileWatchingCfgSource(ICfgSource innerSource, string filePath, TimeSpan? debounceInterval)
    {
        ArgumentNullException.ThrowIfNull(innerSource);
        ArgumentNullException.ThrowIfNull(filePath);
        _innerSource = innerSource;
        _filePath = filePath;
        _debounceInterval = debounceInterval;
    }

    public async ValueTask<ICfgProvider> OpenAsync(CancellationToken ct = default)
    {
        var inner = await _innerSource.OpenAsync(ct);
        return new FileWatchingCfgProvider(inner, _filePath, _debounceInterval);
    }
}
