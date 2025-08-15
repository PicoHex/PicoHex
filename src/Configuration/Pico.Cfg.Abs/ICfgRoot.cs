namespace Pico.Cfg.Abs;

public interface ICfgRoot : ICfgNode
{
    ValueTask ReloadAsync(CancellationToken ct = default);
    IReadOnlyList<ICfgProvider> Providers { get; }
}
