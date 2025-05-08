namespace Pico.Cfg;

public class StreamCfgProvider(Func<Stream> streamFactory) : ICfgProvider
{
    private Dictionary<string, string> _configData = new();
    private readonly StreamChangeToken _changeToken = new();

    public async ValueTask LoadAsync(CancellationToken ct = default)
    {
        await using var stream = streamFactory();
        using var reader = new StreamReader(stream);

        var newData = new Dictionary<string, string>();
        while (await reader.ReadLineAsync(ct) is { } line)
        {
            if (string.IsNullOrWhiteSpace(line))
                continue;

            var pair = line.Split('=', 2);
            if (pair.Length is 2)
                newData[pair[0].Trim()] = pair[1].Trim();
        }

        _configData = newData;
        _changeToken.NotifyChanged();
    }

    public ValueTask<string?> GetValueAsync(string key, CancellationToken ct = default) =>
        ValueTask.FromResult(_configData.GetValueOrDefault(key));

    public async IAsyncEnumerable<ICfgNode> GetChildrenAsync(
        [EnumeratorCancellation] CancellationToken ct = default
    )
    {
        await Task.CompletedTask;
        yield break;
    }

    public ValueTask<IAsyncChangeToken> WatchAsync(CancellationToken ct = default) =>
        ValueTask.FromResult<IAsyncChangeToken>(_changeToken);
}
