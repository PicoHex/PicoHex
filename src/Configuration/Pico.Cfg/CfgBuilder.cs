namespace Pico.CFG;

internal class CFGBuilder : ICFGBuilder
{
    private readonly List<ICFGSource> _sources = [];

    public ICFGBuilder AddSource(ICFGSource source)
    {
        _sources.Add(source);
        return this;
    }

    public async ValueTask<ICFGRoot> BuildAsync(CancellationToken ct = default)
    {
        var providers = new List<ICFGProvider>();
        foreach (var source in _sources)
            providers.Add(await source.BuildProviderAsync(ct));
        return new CFGRoot(providers);
    }
}
