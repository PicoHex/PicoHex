namespace Pico.Cfg.Abs;

public interface ICfgSource
{
    ValueTask<ICfgProvider> BuildProviderAsync(CancellationToken ct = default);
}
