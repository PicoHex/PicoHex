namespace PicoCfg.Tests;

public class CfgRuntimeDependencyWiringTests
{
    [Test]
    public async Task BuildAsync_WithInjectedChangeSignalFactory_UsesCustomRootSignals()
    {
        var initialSignal = new CfgChangeSignal();
        var nextSignal = new CfgChangeSignal();
        var signals = new Queue<CfgChangeSignal>([initialSignal, nextSignal]);
        var provider = new SequenceProvider(
            new CfgSnapshot(new Dictionary<string, string> { ["key"] = "before" }),
            new CfgSnapshot(new Dictionary<string, string> { ["key"] = "after" })
        );
        var builder = Cfg
            .CreateBuilder()
            .WithChangeSignalFactory(() => signals.Dequeue())
            .AddSource(new StaticSource(provider));

        await using var root = await builder.BuildAsync();

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var waitTask = root.WaitForChangeAsync(cts.Token).AsTask();

        var changed = await root.ReloadAsync();

        await Assert.That(changed).IsTrue();
        await waitTask;
        await Assert.That(initialSignal.HasChanged).IsTrue();
        await Assert.That(nextSignal.HasChanged).IsFalse();
    }

    [Test]
    public async Task BuildAsync_DefaultComposer_UsesInjectedSnapshotFactoryForFlattenedNativeSnapshots()
    {
        var composedSnapshot = new CfgSnapshot(new Dictionary<string, string> { ["composed"] = "from-factory" });
        var builder = Cfg
            .CreateBuilder()
            .WithSnapshotFactory((_, _) => composedSnapshot);

        builder.Add(new Dictionary<string, string> { ["first"] = "one" });
        builder.Add(new Dictionary<string, string> { ["second"] = "two" });

        await using var root = await builder.BuildAsync();

        await Assert.That(root.GetValue("composed")).IsEqualTo("from-factory");
    }

    [Test]
    public async Task BuildAsync_WithInjectedProviderStateFactory_UsesCustomProviderStateForBuiltInDictionarySource()
    {
        var publishedSnapshot = new CfgSnapshot(new Dictionary<string, string> { ["key"] = "from-factory" });
        var builder = Cfg
            .CreateBuilder()
            .WithProviderStateFactory(() =>
                TestCfgFactory.CreateProviderState(snapshotFactory: (_, _) => publishedSnapshot)
            );

        builder.Add(new Dictionary<string, string> { ["key"] = "from-source" });

        await using var root = await builder.BuildAsync();

        await Assert.That(root.GetValue("key")).IsEqualTo("from-factory");
    }

    [Test]
    public async Task DictionaryCfgProvider_WithInjectedProviderStateFactory_UsesExplicitStateDependencies()
    {
        var publishedSnapshot = new CfgSnapshot(new Dictionary<string, string> { ["key"] = "from-factory" });
        var initialSignal = new CfgChangeSignal();
        var nextSignal = new CfgChangeSignal();
        var signals = new Queue<CfgChangeSignal>([initialSignal, nextSignal]);
        var provider = TestCfgFactory.CreateDictionaryProvider(
            () => new Dictionary<string, string> { ["key"] = "from-source" },
            versionStampFactory: null,
            TestCfgFactory.CreateProviderState(() => signals.Dequeue(), (_, _) => publishedSnapshot)
        );

        var changed = await provider.ReloadAsync();

        await Assert.That(changed).IsTrue();
        await Assert.That(provider.Snapshot).IsSameReferenceAs(publishedSnapshot);
        await Assert.That(provider.Snapshot.GetValue("key")).IsEqualTo("from-factory");
        await Assert.That(initialSignal.HasChanged).IsTrue();
        await Assert.That(nextSignal.HasChanged).IsFalse();
    }

    private sealed class StaticSource(ICfgProvider provider) : ICfgSource
    {
        public ValueTask<ICfgProvider> OpenAsync(CancellationToken ct = default) =>
            ValueTask.FromResult(provider);
    }

    private sealed class SequenceProvider(params ICfgSnapshot[] snapshots) : ICfgProvider
    {
        private readonly IReadOnlyList<ICfgSnapshot> _snapshots = snapshots;
        private int _index;

        public ICfgSnapshot Snapshot { get; private set; } = snapshots[0];

        public ValueTask<bool> ReloadAsync(CancellationToken ct = default)
        {
            if (_index >= _snapshots.Count - 1)
                return ValueTask.FromResult(false);

            _index++;
            Snapshot = _snapshots[_index];
            return ValueTask.FromResult(true);
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
