namespace Pico.Cfg.Abstractions;

public interface ICfgSource
{
    ValueTask<ICfgProvider> BuildProviderAsync(CancellationToken ct = default);
}
