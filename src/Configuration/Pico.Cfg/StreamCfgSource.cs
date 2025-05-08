namespace Pico.Cfg;

public class StreamCfgSource(Func<Stream> streamFactory) : ICfgSource
{
    public async ValueTask<ICfgProvider> BuildProviderAsync(CancellationToken ct = default)
    {
        var provider = new StreamCfgProvider(streamFactory);
        await provider.LoadAsync(ct);
        return provider;
    }
}
