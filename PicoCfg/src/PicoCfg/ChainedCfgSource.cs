namespace PicoCfg;

internal sealed class ChainedCfgProvider : ICfgProvider
{
    private readonly ICfg _chainedConfig;
    private readonly CfgProviderState _state;
    private readonly ChainedSnapshot _snapshot;

    internal ChainedCfgProvider(ICfg chainedConfig, CfgProviderState state)
    {
        ArgumentNullException.ThrowIfNull(chainedConfig);
        ArgumentNullException.ThrowIfNull(state);
        _chainedConfig = chainedConfig;
        _state = state;
        _snapshot = new ChainedSnapshot(chainedConfig);
    }

    public ICfgSnapshot Snapshot => _snapshot;

    public ValueTask<bool> ReloadAsync(CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        var values = CfgEnumerationExtensions.GetAll(_chainedConfig);
        return ValueTask.FromResult(_state.PublishIfChanged(values, null));
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}

internal sealed class ChainedSnapshot : ICfgSnapshot
{
    private readonly ICfg _chainedConfig;

    internal ChainedSnapshot(ICfg chainedConfig)
    {
        ArgumentNullException.ThrowIfNull(chainedConfig);
        _chainedConfig = chainedConfig;
    }

    public bool TryGetValue(string path, out string? value) =>
        _chainedConfig.TryGetValue(path, out value);
}

internal sealed class ChainedCfgSource : ICfgSource
{
    private readonly Func<ChainedCfgProvider> _providerFactory;

    internal ChainedCfgSource(Func<ChainedCfgProvider> providerFactory)
    {
        ArgumentNullException.ThrowIfNull(providerFactory);
        _providerFactory = providerFactory;
    }

    public async ValueTask<ICfgProvider> OpenAsync(CancellationToken ct = default)
    {
        return await CfgSourceHelpers.OpenAsync(_providerFactory(), ct);
    }
}
