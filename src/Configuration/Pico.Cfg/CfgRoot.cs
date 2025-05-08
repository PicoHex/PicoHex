namespace Pico.Cfg;

internal class CfgRoot(IEnumerable<ICfgProvider> providers) : ICfgRoot
{
    private readonly List<ICfgProvider> _providers = [.. providers];
    private readonly Lock _syncRoot = new();
    private CompositeChangeToken _currentChangeToken = new([]);

    public IReadOnlyList<ICfgProvider> Providers => _providers;

    public async ValueTask ReloadAsync(CancellationToken ct = default)
    {
        foreach (var provider in _providers)
            await provider.LoadAsync(ct);
        UpdateChangeToken();
    }

    public async ValueTask<string?> GetValueAsync(string key, CancellationToken ct = default)
    {
        foreach (var provider in Enumerable.Reverse(_providers))
        {
            var value = await provider.GetValueAsync(key, ct);
            if (value != null)
                return value;
        }
        return null;
    }

    public async IAsyncEnumerable<ICfgNode> GetChildrenAsync(
        [EnumeratorCancellation] CancellationToken ct = default
    )
    {
        foreach (var provider in _providers)
        await foreach (var child in provider.GetChildrenAsync(ct))
            yield return child;
    }

    public ValueTask<IAsyncChangeToken> WatchAsync(CancellationToken ct = default)
    {
        lock (_syncRoot)
            return ValueTask.FromResult<IAsyncChangeToken>(_currentChangeToken);
    }

    private void UpdateChangeToken()
    {
        lock (_syncRoot)
        {
            var tokens = new List<IAsyncChangeToken>();
            foreach (var watchTask in _providers.Select(provider => provider.WatchAsync()))
            {
                if (!watchTask.IsCompleted)
                    watchTask.AsTask().Wait();
                tokens.Add(watchTask.Result);
            }

            _currentChangeToken = new CompositeChangeToken(tokens);
        }
    }

    private class CompositeChangeToken(IReadOnlyList<IAsyncChangeToken> tokens) : IAsyncChangeToken
    {
        public bool HasChanged => tokens.Any(t => t.HasChanged);

        public async ValueTask WaitForChangeAsync(CancellationToken ct = default)
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            var tasks = new List<Task> { Task.Delay(Timeout.Infinite, cts.Token) };
            tasks.AddRange(tokens.Select(token => token.WaitForChangeAsync(cts.Token).AsTask()));

            var completedTask = await Task.WhenAny(tasks);
            await cts.CancelAsync();
            await completedTask;
        }
    }
}
