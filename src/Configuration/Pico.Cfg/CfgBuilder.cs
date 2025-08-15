namespace Pico.Cfg;

internal class CfgBuilder : ICfgBuilder
{
    private readonly List<ICfgSource> _sources = [];

    public ICfgBuilder AddSource(ICfgSource source)
    {
        _sources.Add(source);
        return this;
    }

    public async ValueTask<ICfgRoot> BuildAsync(CancellationToken ct = default)
    {
        var providers = new List<ICfgProvider>();
        foreach (var source in _sources)
            providers.Add(await source.BuildProviderAsync(ct));
        return new CfgRoot(providers);
    }
}
