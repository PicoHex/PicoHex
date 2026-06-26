namespace PicoCfg;

internal sealed class EnvCfgProvider : ICfgProvider
{
    private readonly string? _prefix;
    private readonly CfgProviderState _state;

    internal EnvCfgProvider(string? prefix, CfgProviderState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        _prefix = prefix;
        _state = state;
    }

    public ICfgSnapshot Snapshot => _state.Snapshot;

    public ValueTask<bool> ReloadAsync(CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        var newData = LoadEnvData();
        var versionStamp = Environment.TickCount64 ^ newData.Count;

        if (!_state.TryBeginReload(() => versionStamp, ct, out var candidateVersionStamp))
            return ValueTask.FromResult(false);

        return ValueTask.FromResult(_state.PublishIfChanged(newData, candidateVersionStamp));
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private Dictionary<string, string> LoadEnvData()
    {
        var vars = Environment.GetEnvironmentVariables();
        var newData = new Dictionary<string, string>(vars.Count);
        var enumerator = vars.GetEnumerator();

        while (enumerator.MoveNext())
        {
            var keyStr = (string)enumerator.Key;
            var valueStr = (string?)enumerator.Value;

            if (
                _prefix is not null
                && !keyStr.StartsWith(_prefix, StringComparison.OrdinalIgnoreCase)
            )
                continue;

            var configKey = _prefix is not null
                ? keyStr[_prefix.Length..].Replace("__", ":")
                : keyStr;

            newData[configKey] = valueStr ?? string.Empty;
        }

        return newData;
    }
}

internal sealed class EnvCfgSource : ICfgSource
{
    private readonly Func<EnvCfgProvider> _providerFactory;

    internal EnvCfgSource(Func<EnvCfgProvider> providerFactory)
    {
        ArgumentNullException.ThrowIfNull(providerFactory);
        _providerFactory = providerFactory;
    }

    public async ValueTask<ICfgProvider> OpenAsync(CancellationToken ct = default)
    {
        return await CfgSourceHelpers.OpenAsync(_providerFactory(), ct);
    }
}
