namespace PicoCfg;

internal sealed class StreamCfgSource : ICfgSource
{
    private readonly Func<StreamCfgProvider> _providerFactory;

    internal StreamCfgSource(Func<StreamCfgProvider> providerFactory)
    {
        ArgumentNullException.ThrowIfNull(providerFactory);
        _providerFactory = providerFactory;
    }

    public async ValueTask<ICfgProvider> OpenAsync(CancellationToken ct = default)
    {
        return await CfgSourceHelpers.OpenAsync(_providerFactory(), ct);
    }
}

internal sealed class StreamCfgProvider : ICfgProvider
{
    private readonly Func<CancellationToken, ValueTask<Stream>> _streamFactory;
    private readonly Func<object?>? _versionStampFactory;
    private readonly Func<
        Stream,
        CancellationToken,
        Task<Dictionary<string, string>>
    > _streamParser;
    private readonly CfgProviderState _state;

    internal StreamCfgProvider(
        Func<CancellationToken, ValueTask<Stream>> streamFactory,
        Func<object?>? versionStampFactory,
        Func<Stream, CancellationToken, Task<Dictionary<string, string>>> streamParser,
        CfgProviderState state
    )
    {
        ArgumentNullException.ThrowIfNull(streamFactory);
        ArgumentNullException.ThrowIfNull(streamParser);
        ArgumentNullException.ThrowIfNull(state);
        _streamFactory = streamFactory;
        _versionStampFactory = versionStampFactory;
        _streamParser = streamParser;
        _state = state;
    }

    public ICfgSnapshot Snapshot => _state.Snapshot;

    public async ValueTask<bool> ReloadAsync(CancellationToken ct = default)
    {
        if (!_state.TryBeginReload(_versionStampFactory, ct, out var candidateVersionStamp))
            return false;

        var newData = await CreateSnapshotDataAsync(ct);
        return _state.PublishIfChanged(newData, candidateVersionStamp);
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private async Task<Dictionary<string, string>> CreateSnapshotDataAsync(CancellationToken ct)
    {
        var stream = await _streamFactory(ct).ConfigureAwait(false);

        if (stream is null)
            throw new InvalidOperationException("The stream factory returned null.");

        await using (stream)
        {
            return await _streamParser(stream, ct);
        }
    }
}
