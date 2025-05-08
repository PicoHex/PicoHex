namespace Pico.Cfg.Abstractions;

public interface ICfgRoot : ICfgNode
{
    ValueTask ReloadAsync(CancellationToken ct = default);
    IReadOnlyList<ICfgProvider> Providers { get; }
}
