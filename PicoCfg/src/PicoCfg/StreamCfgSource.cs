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
