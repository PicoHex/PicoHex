namespace Pico.Cfg.Abs;

public interface ICfgBuilder
{
    ICfgBuilder AddSource(ICfgSource source);
    ValueTask<ICfgRoot> BuildAsync(CancellationToken ct = default);
}
