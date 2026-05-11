namespace PicoCfg.Tests;

public class DictionaryCfgTests
{
    [Test]
    public async Task DictionaryCfgProvider_ReloadAsync_WithVersionStampUnchanged_SkipsDataFactory()
    {
        var calls = 0;
        var value = "before";
        var provider = TestCfgFactory.CreateDictionaryProvider(
            () =>
            {
                calls++;
                return new Dictionary<string, string> { ["key"] = value };
            },
            () => 1
        );

        var initialChanged = await provider.ReloadAsync();
        value = "after";
        var changed = await provider.ReloadAsync();

        await Assert.That(initialChanged).IsTrue();
        await Assert.That(changed).IsFalse();
        await Assert.That(calls).IsEqualTo(1);
        await Assert.That(provider.Snapshot.GetValue("key")).IsEqualTo("before");
    }

    [Test]
    public async Task DictionaryCfgProvider_ReloadAsync_WithNullVersionStampOnFirstLoad_DoesNotSkipInitialLoad()
    {
        var calls = 0;
        var provider = TestCfgFactory.CreateDictionaryProvider(
            () =>
            {
                calls++;
                return new Dictionary<string, string> { ["key"] = "value" };
            },
            () => null
        );

        var initialChanged = await provider.ReloadAsync();
        var initialSnapshot = provider.Snapshot;
        var changed = await provider.ReloadAsync();

        await Assert.That(initialChanged).IsTrue();
        await Assert.That(changed).IsFalse();
        await Assert.That(calls).IsEqualTo(1);
        await Assert.That(provider.Snapshot).IsSameReferenceAs(initialSnapshot);
        await Assert.That(provider.Snapshot.GetValue("key")).IsEqualTo("value");
    }

    [Test]
    public async Task DictionaryCfgSource_WithVersionStampUnchanged_SkipsReloadWork()
    {
        var calls = 0;
        var value = "before";
        var source = TestCfgFactory.CreateDictionarySource(
            () =>
            {
                calls++;
                return new Dictionary<string, string> { ["key"] = value };
            },
            () => 1
        );

        var provider = await source.OpenAsync();
        value = "after";
        var changed = await provider.ReloadAsync();

        await Assert.That(changed).IsFalse();
        await Assert.That(calls).IsEqualTo(1);
        await Assert.That(provider.Snapshot.GetValue("key")).IsEqualTo("before");
    }

    [Test]
    public async Task DictionaryCfgSource_OpenAsync_WithNullVersionStamp_LoadsInitialSnapshot()
    {
        var calls = 0;
        var source = TestCfgFactory.CreateDictionarySource(
            () =>
            {
                calls++;
                return new Dictionary<string, string> { ["key"] = "value" };
            },
            () => null
        );

        var provider = await source.OpenAsync();
        var snapshot = provider.Snapshot;
        var changed = await provider.ReloadAsync();

        await Assert.That(snapshot.GetValue("key")).IsEqualTo("value");
        await Assert.That(changed).IsFalse();
        await Assert.That(calls).IsEqualTo(1);
    }

    [Test]
    public async Task DictionaryCfgProvider_ReloadAsync_WithChangedVersionStampAndSameContent_KeepsSnapshot()
    {
        var stamp = 1;
        var provider = TestCfgFactory.CreateDictionaryProvider(
            () => new Dictionary<string, string> { ["key"] = "value" },
            () => stamp
        );

        var initialChanged = await provider.ReloadAsync();
        var originalSnapshot = provider.Snapshot;
        stamp = 2;
        var changed = await provider.ReloadAsync();

        await Assert.That(initialChanged).IsTrue();
        await Assert.That(changed).IsFalse();
        await Assert.That(provider.Snapshot).IsSameReferenceAs(originalSnapshot);
    }

    [Test]
    public async Task DictionaryCfgProvider_ReloadAsync_WithAcceptedNullStamp_TreatsRepeatedNullAsAuthoritativeShortcut()
    {
        var calls = 0;
        var value = "before";
        var provider = TestCfgFactory.CreateDictionaryProvider(
            () =>
            {
                calls++;
                return new Dictionary<string, string> { ["key"] = value };
            },
            () => null
        );

        var initialChanged = await provider.ReloadAsync();
        value = "after";
        var changed = await provider.ReloadAsync();

        await Assert.That(initialChanged).IsTrue();
        await Assert.That(changed).IsFalse();
        await Assert.That(calls).IsEqualTo(1);
        await Assert.That(provider.Snapshot.GetValue("key")).IsEqualTo("before");
    }

    [Test]
    public async Task DictionaryCfgProvider_ReloadAsync_WithChangedStampAndSameContent_UpdatesAuthorityBaseline()
    {
        var calls = 0;
        var stamp = 1;
        var value = "before";
        var provider = TestCfgFactory.CreateDictionaryProvider(
            () =>
            {
                calls++;
                return new Dictionary<string, string> { ["key"] = value };
            },
            () => stamp
        );

        var initialChanged = await provider.ReloadAsync();
        var originalSnapshot = provider.Snapshot;
        stamp = 2;
        var unchanged = await provider.ReloadAsync();
        value = "after";
        var laterShortcut = await provider.ReloadAsync();

        await Assert.That(initialChanged).IsTrue();
        await Assert.That(unchanged).IsFalse();
        await Assert.That(laterShortcut).IsFalse();
        await Assert.That(calls).IsEqualTo(2);
        await Assert.That(provider.Snapshot).IsSameReferenceAs(originalSnapshot);
        await Assert.That(provider.Snapshot.GetValue("key")).IsEqualTo("before");
    }

    [Test]
    public async Task DictionaryCfgProvider_ReloadAsync_WithChangedVersionStampAndChangedContent_PublishesNewSnapshot()
    {
        var stamp = 1;
        var value = "before";
        var provider = TestCfgFactory.CreateDictionaryProvider(
            () => new Dictionary<string, string> { ["key"] = value },
            () => stamp
        );

        var initialChanged = await provider.ReloadAsync();
        var originalSnapshot = provider.Snapshot;
        stamp = 2;
        value = "after";
        var changed = await provider.ReloadAsync();

        await Assert.That(initialChanged).IsTrue();
        await Assert.That(changed).IsTrue();
        await Assert.That(provider.Snapshot).IsNotSameReferenceAs(originalSnapshot);
        await Assert.That(provider.Snapshot.GetValue("key")).IsEqualTo("after");
    }

    [Test]
    public async Task DictionaryCfgProvider_ReloadAsync_WithDuplicateKeysAndSameVisibleState_KeepsSnapshot()
    {
        var items = new[]
        {
            new KeyValuePair<string, string>("key", "first"),
            new KeyValuePair<string, string>("key", "value"),
        };
        var provider = TestCfgFactory.CreateDictionaryProvider(() => items);

        var initialChanged = await provider.ReloadAsync();
        var originalSnapshot = provider.Snapshot;

        items =
        [
            new KeyValuePair<string, string>("key", "different"),
            new KeyValuePair<string, string>("key", "value"),
        ];

        var changed = await provider.ReloadAsync();

        await Assert.That(initialChanged).IsTrue();
        await Assert.That(changed).IsFalse();
        await Assert.That(provider.Snapshot).IsSameReferenceAs(originalSnapshot);
        await Assert.That(provider.Snapshot.GetValue("key")).IsEqualTo("value");
    }

    [Test]
    public async Task DictionaryCfgProvider_ReloadAsync_CallsVersionStampFactoryOutsideLock()
    {
        DictionaryCfgProvider? provider = null;
        var versionStampEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var allowVersionStampToExit = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var snapshotReadCompleted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        provider = TestCfgFactory.CreateDictionaryProvider(
            () => new Dictionary<string, string> { ["key"] = "value" },
            () =>
            {
                versionStampEntered.TrySetResult();
                _ = Task.Run(() =>
                {
                    _ = provider!.Snapshot.GetValue("key");
                    snapshotReadCompleted.TrySetResult();
                });
                allowVersionStampToExit.Task.GetAwaiter().GetResult();

                return 1;
            }
        );

        var reloadTask = Task.Run(async () => await provider.ReloadAsync());
        await versionStampEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await snapshotReadCompleted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        allowVersionStampToExit.TrySetResult();

        var changed = await reloadTask;

        await Assert.That(changed).IsTrue();
    }

    [Test]
    public async Task DictionaryCfgProvider_ReloadAsync_WithPreCancelledToken_DoesNotInvokeFactories()
    {
        var dataFactoryCalls = 0;
        var versionStampCalls = 0;
        var provider = TestCfgFactory.CreateDictionaryProvider(
            () =>
            {
                dataFactoryCalls++;
                return new Dictionary<string, string> { ["key"] = "value" };
            },
            () =>
            {
                versionStampCalls++;
                return 1;
            }
        );

        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.That(async () => await provider.ReloadAsync(cts.Token)).Throws<OperationCanceledException>();
        await Assert.That(dataFactoryCalls).IsEqualTo(0);
        await Assert.That(versionStampCalls).IsEqualTo(0);
    }

    [Test]
    public async Task DictionaryCfgProvider_ReloadAsync_CancellationAfterVersionStamp_DoesNotInvokeDataFactory()
    {
        var dataFactoryCalls = 0;
        CancellationTokenSource? cancellationSource = null;
        var provider = TestCfgFactory.CreateDictionaryProvider(
            () =>
            {
                dataFactoryCalls++;
                return [];
            },
            () =>
            {
                cancellationSource!.Cancel();
                return 1;
            }
        );

        using var cts = new CancellationTokenSource();
        cancellationSource = cts;

        await Assert.That(async () => await provider.ReloadAsync(cts.Token)).Throws<OperationCanceledException>();
        await Assert.That(dataFactoryCalls).IsEqualTo(0);
        await Assert.That(provider.Snapshot).IsSameReferenceAs(CfgSnapshot.Empty);
    }

    [Test]
    public async Task DictionaryCfgProvider_ReloadAsync_WhenEnumerationIsCancelled_DoesNotPublishSnapshot()
    {
        using var cts = new CancellationTokenSource();
        var sequence = new BlockingSequence(
            first: new KeyValuePair<string, string>("key", "before"),
            second: new KeyValuePair<string, string>("key", "after"),
            onFirstYield: () => cts.Cancel()
        );
        var provider = TestCfgFactory.CreateDictionaryProvider(() => sequence);

        await Assert.That(async () => await provider.ReloadAsync(cts.Token)).Throws<OperationCanceledException>();
        await Assert.That(provider.Snapshot).IsSameReferenceAs(CfgSnapshot.Empty);
        await Assert.That(provider.Snapshot.GetValue("key")).IsNull();
    }

    private sealed class BlockingSequence(
        KeyValuePair<string, string> first,
        KeyValuePair<string, string> second,
        Action onFirstYield
    ) : IEnumerable<KeyValuePair<string, string>>
    {
        public IEnumerator<KeyValuePair<string, string>> GetEnumerator()
        {
            onFirstYield();
            yield return first;
            yield return second;
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
