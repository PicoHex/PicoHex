namespace PicoCfg;

internal sealed class KeyPerFileCfgProvider : ICfgProvider
{
    private readonly string _directoryPath;
    private readonly Func<string, bool>? _keyFilter;
    private readonly CfgProviderState _state;

    internal KeyPerFileCfgProvider(
        string directoryPath,
        Func<string, bool>? keyFilter,
        CfgProviderState state
    )
    {
        ArgumentNullException.ThrowIfNull(directoryPath);
        ArgumentNullException.ThrowIfNull(state);
        _directoryPath = directoryPath;
        _keyFilter = keyFilter;
        _state = state;
    }

    public ICfgSnapshot Snapshot => _state.Snapshot;

    public async ValueTask<bool> ReloadAsync(CancellationToken ct = default)
    {
        if (!_state.TryBeginReload(CreateVersionStamp, ct, out var candidateVersionStamp))
            return false;

        var newData = await CreateSnapshotDataAsync(ct).ConfigureAwait(false);
        return _state.PublishIfChanged(newData, candidateVersionStamp);
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private object? CreateVersionStamp()
    {
        if (!Directory.Exists(_directoryPath))
            return null;

        var fileCount = Directory.EnumerateFiles(_directoryPath).Count();
        return Directory.GetLastWriteTimeUtc(_directoryPath).Ticks ^ fileCount;
    }

    private async Task<Dictionary<string, string>> CreateSnapshotDataAsync(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        if (!Directory.Exists(_directoryPath))
            return new Dictionary<string, string>();

        var filter = _keyFilter ?? (f => !Path.GetFileName(f).StartsWith('.'));
        var newData = new Dictionary<string, string>();

        foreach (var file in Directory.EnumerateFiles(_directoryPath))
        {
            ct.ThrowIfCancellationRequested();

            if (!filter(file))
                continue;

            var key = Path.GetFileName(file);
            var value = (
                await File.ReadAllTextAsync(file, Encoding.UTF8, ct).ConfigureAwait(false)
            ).TrimEnd('\r', '\n');
            newData[key] = value;
        }

        return newData;
    }
}

internal sealed class KeyPerFileCfgSource : ICfgSource
{
    private readonly Func<KeyPerFileCfgProvider> _providerFactory;

    internal KeyPerFileCfgSource(Func<KeyPerFileCfgProvider> providerFactory)
    {
        ArgumentNullException.ThrowIfNull(providerFactory);
        _providerFactory = providerFactory;
    }

    public async ValueTask<ICfgProvider> OpenAsync(CancellationToken ct = default)
    {
        return await CfgSourceHelpers.OpenAsync(_providerFactory(), ct);
    }
}
