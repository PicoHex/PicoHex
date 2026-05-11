namespace PicoCfg;

internal sealed class DictionaryCfgProvider : ICfgProvider
{
    private readonly Func<IEnumerable<KeyValuePair<string, string>>> _dataFactory;
    private readonly Func<object?>? _versionStampFactory;
    private readonly CfgProviderState _state;

    internal DictionaryCfgProvider(
        Func<IEnumerable<KeyValuePair<string, string>>> dataFactory,
        Func<object?>? versionStampFactory,
        CfgProviderState state
    )
    {
        ArgumentNullException.ThrowIfNull(dataFactory);
        ArgumentNullException.ThrowIfNull(state);
        _dataFactory = dataFactory;
        _versionStampFactory = versionStampFactory;
        _state = state;
    }

    public ICfgSnapshot Snapshot => _state.Snapshot;

    public ValueTask<bool> ReloadAsync(CancellationToken ct = default)
    {
        if (!_state.TryBeginReload(_versionStampFactory, ct, out var candidateVersionStamp))
            return ValueTask.FromResult(false);

        var newData = CreateSnapshotData(ct);
        return ValueTask.FromResult(_state.PublishIfChanged(newData, candidateVersionStamp));
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private Dictionary<string, string> CreateSnapshotData(CancellationToken ct)
    {
        var sourceData = _dataFactory();
        var newData = sourceData is ICollection<KeyValuePair<string, string>> collection
            ? new Dictionary<string, string>(collection.Count)
            : sourceData.TryGetNonEnumeratedCount(out var count)
                ? new Dictionary<string, string>(count)
                : new Dictionary<string, string>();

        foreach (var (key, value) in sourceData)
        {
            ct.ThrowIfCancellationRequested();
            newData[key] = value;
        }

        return newData;
    }
}
