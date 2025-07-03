namespace Pico.CFG.Abs;

public interface ICFGRoot : ICFGNode
{
    ValueTask ReloadAsync(CancellationToken ct = default);
    IReadOnlyList<ICFGProvider> Providers { get; }
}
