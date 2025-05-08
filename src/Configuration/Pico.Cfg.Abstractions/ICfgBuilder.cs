namespace Pico.Cfg.Abstractions;

public interface ICfgBuilder
{
    ICfgBuilder AddSource(ICfgSource source);
    ValueTask<ICfgRoot> BuildAsync(CancellationToken ct = default);
}
