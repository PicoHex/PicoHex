namespace Pico.CFG.Abs;

public interface ICFGSource
{
    ValueTask<ICFGProvider> BuildProviderAsync(CancellationToken ct = default);
}
