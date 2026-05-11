namespace PicoCfg;

internal sealed class CmdLineCfgProvider : ICfgProvider
{
    private readonly string[] _args;
    private readonly string? _prefix;
    private readonly CfgProviderState _state;

    internal CmdLineCfgProvider(string[] args, string? prefix, CfgProviderState state)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(state);
        _args = args;
        _prefix = prefix;
        _state = state;
    }

    public ICfgSnapshot Snapshot => _state.Snapshot;

    public ValueTask<bool> ReloadAsync(CancellationToken ct = default)
    {
        if (!_state.TryBeginReload(null, ct, out var candidateVersionStamp))
            return ValueTask.FromResult(false);

        var newData = ParseArgs(_args, _prefix, ct);
        return ValueTask.FromResult(_state.PublishIfChanged(newData, candidateVersionStamp));
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private static Dictionary<string, string> ParseArgs(
        string[] args, string? prefix, CancellationToken ct)
    {
        var result = new Dictionary<string, string>(args.Length);

        for (var i = 0; i < args.Length; i++)
        {
            ct.ThrowIfCancellationRequested();
            var arg = args[i];

            string rawKey;

            if (arg.StartsWith("--", StringComparison.Ordinal))
            {
                rawKey = arg[2..];
            }
            else if (arg.Length > 1 && arg[0] == '-')
            {
                rawKey = arg[1..];
            }
            else if (arg.StartsWith("/", StringComparison.Ordinal))
            {
                rawKey = arg[1..];
            }
            else
            {
                continue;
            }

            if (prefix is not null && !rawKey.StartsWith(prefix, StringComparison.Ordinal))
                continue;

            var eqIndex = rawKey.IndexOf('=');
            string key;
            string value;

            if (eqIndex >= 0)
            {
                key = rawKey[..eqIndex];
                value = rawKey[(eqIndex + 1)..];
            }
            else
            {
                key = rawKey;
                if (i + 1 < args.Length
                    && args[i + 1].Length > 0
                    && args[i + 1][0] != '-'
                    && args[i + 1][0] != '/')
                {
                    value = args[i + 1];
                    i++;
                }
                else
                {
                    value = "true";
                }
            }

            result[key] = value;
        }

        return result;
    }
}

internal sealed class CmdLineCfgSource : ICfgSource
{
    private readonly Func<CmdLineCfgProvider> _providerFactory;

    internal CmdLineCfgSource(Func<CmdLineCfgProvider> providerFactory)
    {
        ArgumentNullException.ThrowIfNull(providerFactory);
        _providerFactory = providerFactory;
    }

    public async ValueTask<ICfgProvider> OpenAsync(CancellationToken ct = default)
    {
        return await CfgSourceHelpers.OpenAsync(_providerFactory(), ct);
    }
}
