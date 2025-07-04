namespace Pico.CFG.Abs;

public interface ICFGProvider : ICFGNode
{
    ValueTask LoadAsync(CancellationToken ct = default);
}
