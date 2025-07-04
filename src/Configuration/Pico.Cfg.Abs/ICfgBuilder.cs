namespace Pico.CFG.Abs;

public interface ICFGBuilder
{
    ICFGBuilder AddSource(ICFGSource source);
    ValueTask<ICFGRoot> BuildAsync(CancellationToken ct = default);
}
