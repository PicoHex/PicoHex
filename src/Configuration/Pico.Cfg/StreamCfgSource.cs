namespace Pico.CFG;

public class StreamCFGSource(Func<Stream> streamFactory) : ICFGSource
{
    public async ValueTask<ICFGProvider> BuildProviderAsync(CancellationToken ct = default)
    {
        var provider = new StreamCFGProvider(streamFactory);
        await provider.LoadAsync(ct);
        return provider;
    }
}
