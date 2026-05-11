namespace PicoCfg;

internal sealed class DictionaryCfgSource : ICfgSource
{
    private readonly Func<DictionaryCfgProvider> _providerFactory;

    internal DictionaryCfgSource(Func<DictionaryCfgProvider> providerFactory)
    {
        ArgumentNullException.ThrowIfNull(providerFactory);
        _providerFactory = providerFactory;
    }

    public async ValueTask<ICfgProvider> OpenAsync(CancellationToken ct = default)
    {
        return await CfgSourceHelpers.OpenAsync(_providerFactory(), ct);
    }
}
