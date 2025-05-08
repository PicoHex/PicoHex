namespace Pico.Cfg.Abstractions;

public interface ICfgNode
{
    ValueTask<string?> GetValueAsync(string key, CancellationToken ct = default);
    IAsyncEnumerable<ICfgNode> GetChildrenAsync(CancellationToken ct = default);
    ValueTask<IAsyncChangeToken> WatchAsync(CancellationToken ct = default);
}
