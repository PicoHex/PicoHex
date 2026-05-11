namespace PicoCfg.Tests;

public sealed class CfgRootConsumerContractTests
{
    [Test]
    public async Task Root_ProvidesCurrentPublishedValues()
    {
        await using var root = await Cfg.CreateBuilder().Add("App:Name=PicoCfg").BuildAsync();

        await Assert.That(root.GetValue("App:Name")).IsEqualTo("PicoCfg");
    }

    [Test]
    public async Task WaitForChangeAsync_CompletesAfterPublishedReload()
    {
        var data = new Dictionary<string, string> { ["App:Name"] = "Before" };
        var version = 0;

        await using var root = await Cfg
            .CreateBuilder()
            .Add(() => data, () => version)
            .BuildAsync();

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var waitTask = root.WaitForChangeAsync(cts.Token).AsTask();

        await Assert.That(waitTask.IsCompleted).IsFalse();

        data = new Dictionary<string, string> { ["App:Name"] = "After" };
        version++;

        var changed = await root.ReloadAsync(cts.Token);

        await waitTask;

        await Assert.That(changed).IsTrue();
        await Assert.That(root.GetValue("App:Name")).IsEqualTo("After");
        await Assert.That(waitTask.IsCompletedSuccessfully).IsTrue();
    }

    [Test]
    public async Task WaitForChangeAsync_UsesCurrentPublishedVersionSignal()
    {
        var provider = new SequenceProvider(
            new InlineSnapshot(new Dictionary<string, string> { ["key"] = "before" }),
            new InlineSnapshot(new Dictionary<string, string> { ["key"] = "after" })
        );
        var root = TestCfgFactory.CreateRoot([provider]);

        var waitTask = root.WaitForChangeAsync().AsTask();

        await root.ReloadAsync();
        await waitTask;

        await Assert.That(waitTask.IsCompletedSuccessfully).IsTrue();
        await root.DisposeAsync();
    }

    private sealed class SequenceProvider(params ICfgSnapshot[] snapshots) : ICfgProvider
    {
        private readonly IReadOnlyList<ICfgSnapshot> _snapshots = snapshots;
        private int _index;
        private CfgChangeSignal _changeSignal = new();

        public ICfgSnapshot Snapshot { get; private set; } = snapshots[0];

        public ValueTask<bool> ReloadAsync(CancellationToken ct = default)
        {
            if (_index >= _snapshots.Count - 1)
                return ValueTask.FromResult(false);

            _index++;
            Snapshot = _snapshots[_index];
            var oldSignal = _changeSignal;
            _changeSignal = new CfgChangeSignal();
            oldSignal.NotifyChanged();
            return ValueTask.FromResult(true);
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class InlineSnapshot(IReadOnlyDictionary<string, string> values) : ICfgSnapshot
    {
        public bool TryGetValue(string path, out string? value)
        {
            if (values.TryGetValue(path, out var resolved))
            {
                value = resolved;
                return true;
            }

            value = null;
            return false;
        }
    }
}
