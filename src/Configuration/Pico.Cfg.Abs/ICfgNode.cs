namespace Pico.CFG.Abs;

public interface ICFGNode
{
    ValueTask<string?> GetValueAsync(string key, CancellationToken ct = default);
    IAsyncEnumerable<ICFGNode> GetChildrenAsync(CancellationToken ct = default);
    ValueTask<IAsyncChangeToken> WatchAsync(CancellationToken ct = default);
}
