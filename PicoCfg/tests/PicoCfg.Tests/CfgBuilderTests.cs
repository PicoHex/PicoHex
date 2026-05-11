namespace PicoCfg.Tests;

public class CfgBuilderTests
{
    private static ICfgSnapshot SnapshotOf(ICfgRoot root) => ((IInternalCfgRootSnapshotAccessor)root).CurrentSnapshot;

    [Test]
    public async Task AddSource_AddsSourceToBuilder()
    {
        var builder = Cfg.CreateBuilder();
        var mockSource = new MockSource();

        var result = builder.AddSource(mockSource);

        await Assert.That(result).IsSameReferenceAs(builder);
    }

    [Test]
    public async Task AddSource_WithNullSource_ThrowsArgumentNullException()
    {
        var builder = Cfg.CreateBuilder();

        await Assert.That(() => builder.AddSource(null!)).Throws<ArgumentNullException>();
    }

    [Test]
    public async Task BuildAsync_WithNoSources_ReturnsRootWithEmptyProviders()
    {
        var builder = Cfg.CreateBuilder();

        var root = await builder.BuildAsync();

        await Assert.That(root).IsNotNull();
        await Assert.That(root.GetValue("missing")).IsNull();
    }

    [Test]
    public async Task BuildAsync_WithOneSource_UsesSourceSnapshot()
    {
        var builder = Cfg.CreateBuilder();
        var mockSource = new MockSource();
        builder.AddSource(mockSource);

        var root = await builder.BuildAsync();

        await Assert.That(root.GetValue("sourceKey")).IsEqualTo("sourceValue");
    }

    [Test]
    public async Task BuildAsync_WithMultipleSources_UsesLastSourceValue()
    {
        var builder = Cfg.CreateBuilder();
        var mockSource1 = new MockSource("shared", "first");
        var mockSource2 = new MockSource("shared", "second");
        builder.AddSource(mockSource1);
        builder.AddSource(mockSource2);

        var root = await builder.BuildAsync();

        await Assert.That(root.GetValue("shared")).IsEqualTo("second");
    }

    [Test]
    public async Task BuildAsync_WhenLaterSourceFails_DisposesAlreadyOpenedProviders()
    {
        var builder = Cfg.CreateBuilder();
        var firstProvider = new TrackingProvider("first", "value");
        var secondProvider = new TrackingProvider("second", "value");

        builder.AddSource(new TrackingSource(firstProvider));
        builder.AddSource(new TrackingSource(secondProvider));
        builder.AddSource(new FailingSource());

        await Assert.That(async () => await builder.BuildAsync()).Throws<InvalidOperationException>();
        await Assert.That(firstProvider.DisposeCalled).IsTrue();
        await Assert.That(secondProvider.DisposeCalled).IsTrue();
    }

    [Test]
    public async Task BuildAsync_WhenLaterSourceIsCancelled_DisposesAlreadyOpenedProviders()
    {
        var builder = Cfg.CreateBuilder();
        var firstProvider = new TrackingProvider("first", "value");

        builder.AddSource(new TrackingSource(firstProvider));
        builder.AddSource(new CancelledSource());

        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.That(async () => await builder.BuildAsync(cts.Token)).Throws<OperationCanceledException>();
        await Assert.That(firstProvider.DisposeCalled).IsTrue();
    }

    [Test]
    public async Task BuildAsync_WithDictionaryFactorySource_UsesLatestFactoryValues()
    {
        var builder = Cfg.CreateBuilder();
        builder.Add(() => new Dictionary<string, string> { ["factoryKey"] = "factoryValue" });

        var root = await builder.BuildAsync();

        await Assert.That(root.GetValue("factoryKey")).IsEqualTo("factoryValue");
    }

    [Test]
    public async Task BuildAsync_RepeatedBuilds_CreateIndependentBuiltInProviderStateInstances()
    {
        var builder = Cfg.CreateBuilder();
        var value = "before";
        var stamp = 1;

        builder.Add(
            () => new Dictionary<string, string> { ["key"] = value },
            () => stamp
        );

        await using var firstRoot = await builder.BuildAsync();
        await using var secondRoot = await builder.BuildAsync();

        await Assert.That(firstRoot.GetValue("key")).IsEqualTo("before");
        await Assert.That(secondRoot.GetValue("key")).IsEqualTo("before");

        stamp = 2;
        value = "after";

        var firstChanged = await firstRoot.ReloadAsync();

        await Assert.That(firstChanged).IsTrue();
        await Assert.That(firstRoot.GetValue("key")).IsEqualTo("after");
        await Assert.That(secondRoot.GetValue("key")).IsEqualTo("before");

        var secondChanged = await secondRoot.ReloadAsync();

        await Assert.That(secondChanged).IsTrue();
        await Assert.That(secondRoot.GetValue("key")).IsEqualTo("after");
    }

    [Test]
    public async Task BuildAsync_WithPublicStreamParser_UsesInjectedStreamParserForBuiltInSourcePath()
    {
        var parserCalls = 0;
        var builder = Cfg
            .CreateBuilder()
            .WithStreamParser(async (stream, ct) =>
            {
                parserCalls++;
                using var reader = new StreamReader(stream);
                var content = await reader.ReadToEndAsync(ct);
                return new Dictionary<string, string> { ["parsed"] = $"custom:{content}" };
            });

        builder.Add(() => new MemoryStream(Encoding.UTF8.GetBytes("not-valid-default-format")));

        await using var root = await builder.BuildAsync();

        await Assert.That(parserCalls).IsEqualTo(1);
        await Assert.That(root.GetValue("parsed")).IsEqualTo("custom:not-valid-default-format");
    }

    [Test]
    public async Task BuildAsync_WithWrappedPublicDefaultStreamParser_PreservesBuiltInParsingBehavior()
    {
        var parserCalls = 0;
        var defaultParser = CfgBuilder.CreateDefaultStreamParser();
        var builder = Cfg
            .CreateBuilder()
            .WithStreamParser(async (stream, ct) =>
            {
                parserCalls++;
                return await defaultParser(stream, ct);
            });

        builder.Add(() => new MemoryStream(Encoding.UTF8.GetBytes(" key = a=b=c \ninvalid\n\nother = value ")));

        await using var root = await builder.BuildAsync();

        await Assert.That(parserCalls).IsEqualTo(1);
        await Assert.That(root.GetValue("key")).IsEqualTo("a=b=c");
        await Assert.That(root.GetValue("other")).IsEqualTo("value");
        await Assert.That(root.GetValue("invalid")).IsNull();
    }

    [Test]
    public async Task BuildAsync_WithPublicSnapshotComposer_UsesCustomComposerForInitialAndReloadedSnapshots()
    {
        var initialSnapshot = new DelegatingSnapshot(path => path == "mode" ? "initial" : null);
        var reloadedSnapshot = new DelegatingSnapshot(path => path == "mode" ? "reloaded" : null);
        var composeCalls = 0;
        var provider = new SequenceProvider(
            new MockSnapshot("provider", "before"),
            new MockSnapshot("provider", "after")
        );
        var builder = Cfg
            .CreateBuilder()
            .WithSnapshotComposer(_ => ++composeCalls == 1 ? initialSnapshot : reloadedSnapshot)
            .AddSource(new StaticSource(provider));

        await using var root = await builder.BuildAsync();

        await Assert.That(SnapshotOf(root)).IsSameReferenceAs(initialSnapshot);

        var changed = await root.ReloadAsync();

        await Assert.That(changed).IsTrue();
        await Assert.That(composeCalls).IsEqualTo(2);
        await Assert.That(SnapshotOf(root)).IsSameReferenceAs(reloadedSnapshot);
    }

    [Test]
    public async Task BuildAsync_WithWrappedPublicDefaultSnapshotComposer_PreservesDefaultCompositionBehavior()
    {
        var composeCalls = 0;
        var defaultComposer = CfgBuilder.CreateDefaultSnapshotComposer();
        var builder = Cfg
            .CreateBuilder()
            .WithSnapshotComposer(providerSnapshots =>
            {
                composeCalls++;
                return defaultComposer(providerSnapshots);
            });

        builder.AddSource(new MockSource("shared", "first"));
        builder.AddSource(new MockSource("other", "value"));
        builder.AddSource(new MockSource("shared", "second"));

        await using var root = await builder.BuildAsync();

        await Assert.That(composeCalls).IsEqualTo(1);
        await Assert.That(root.GetValue("shared")).IsEqualTo("second");
        await Assert.That(root.GetValue("other")).IsEqualTo("value");
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

    private sealed class DelegatingSnapshot(Func<string, string?> resolver) : ICfgSnapshot
    {
        public bool TryGetValue(string path, out string? value)
        {
            value = resolver(path);
            return value is not null;
        }
    }

    private class MockSource(string key = "sourceKey", string value = "sourceValue") : ICfgSource
    {
        public ValueTask<ICfgProvider> OpenAsync(CancellationToken ct = default)
        {
            return ValueTask.FromResult<ICfgProvider>(new MockProvider(key, value));
        }
    }

    private class MockProvider(string key, string value) : ICfgProvider
    {
        public ICfgSnapshot Snapshot { get; private set; } = new MockSnapshot(key, value);

        public ValueTask<bool> ReloadAsync(CancellationToken ct = default)
        {
            return ValueTask.FromResult(false);
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private class MockSnapshot(string key, string value) : ICfgSnapshot
    {
        public bool TryGetValue(string path, out string? resolvedValue)
        {
            if (path == key)
            {
                resolvedValue = value;
                return true;
            }

            resolvedValue = null;
            return false;
        }
    }

    private sealed class TrackingSource(TrackingProvider provider) : ICfgSource
    {
        public ValueTask<ICfgProvider> OpenAsync(CancellationToken ct = default)
        {
            return ValueTask.FromResult<ICfgProvider>(provider);
        }
    }

    private sealed class FailingSource : ICfgSource
    {
        public ValueTask<ICfgProvider> OpenAsync(CancellationToken ct = default)
        {
            throw new InvalidOperationException("Source open failed.");
        }
    }

    private sealed class CancelledSource : ICfgSource
    {
        public ValueTask<ICfgProvider> OpenAsync(CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            return ValueTask.FromCanceled<ICfgProvider>(ct);
        }
    }

    private sealed class TrackingProvider(string key, string value) : ICfgProvider
    {
        public bool DisposeCalled { get; private set; }
        public ICfgSnapshot Snapshot { get; } = new MockSnapshot(key, value);

        public ValueTask<bool> ReloadAsync(CancellationToken ct = default)
        {
            return ValueTask.FromResult(false);
        }

        public ValueTask DisposeAsync()
        {
            DisposeCalled = true;
            return ValueTask.CompletedTask;
        }
    }
}
