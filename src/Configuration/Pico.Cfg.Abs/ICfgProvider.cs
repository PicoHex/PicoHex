namespace Pico.Cfg.Abs;

public interface ICfgProvider : ICfgNode
{
    ValueTask LoadAsync(CancellationToken ct = default);
}
