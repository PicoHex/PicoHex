namespace PicoCfg.Tests;

public class StreamCfgTests
{
    [Test]
    public async Task StreamCfgSource_WithNullFactory_ThrowsArgumentNullException()
    {
        await Assert.That(() => new StreamCfgSource(null!)).Throws<ArgumentNullException>();
    }

    [Test]
    public async Task StreamCfgProvider_WithNullFactory_ThrowsArgumentNullException()
    {
        await Assert
            .That(
                () =>
                    TestCfgFactory.CreateStreamProvider(
                        null!,
                        streamParser: CfgBuilder.CreateDefaultStreamParser(),
                        state: TestCfgFactory.CreateProviderState()
                    )
            )
            .Throws<ArgumentNullException>();
    }

    [Test]
    public async Task StreamCfgSource_OpenAsync_ReturnsProvider()
    {
        var streamFactory = () =>
            new MemoryStream(Encoding.UTF8.GetBytes("key1=value1\nkey2=value2"));
        var source = TestCfgFactory.CreateStreamSource(streamFactory);

        var provider = await source.OpenAsync();

        await Assert.That(provider).IsNotNull();
        await Assert.That(provider).IsAssignableTo<ICfgProvider>();
    }

    [Test]
    public async Task StreamCfgProvider_ReloadAsync_ParsesKeyValuePairs()
    {
        var streamFactory = () =>
            new MemoryStream(Encoding.UTF8.GetBytes("key1=value1\nkey2=value2\nkey3=value3"));
        var provider = TestCfgFactory.CreateStreamProvider(streamFactory);

        var changed = await provider.ReloadAsync();

        await Assert.That(changed).IsTrue();
        var value1 = provider.Snapshot.GetValue("key1");
        var value2 = provider.Snapshot.GetValue("key2");
        var value3 = provider.Snapshot.GetValue("key3");

        await Assert.That(value1).IsEqualTo("value1");
        await Assert.That(value2).IsEqualTo("value2");
        await Assert.That(value3).IsEqualTo("value3");
    }

    [Test]
    public async Task StreamCfgProvider_ReloadAsync_IgnoresEmptyLines()
    {
        var streamFactory = () =>
            new MemoryStream(Encoding.UTF8.GetBytes("\nkey1=value1\n\nkey2=value2\n"));
        var provider = TestCfgFactory.CreateStreamProvider(streamFactory);

        _ = await provider.ReloadAsync();

        var value1 = provider.Snapshot.GetValue("key1");
        var value2 = provider.Snapshot.GetValue("key2");

        await Assert.That(value1).IsEqualTo("value1");
        await Assert.That(value2).IsEqualTo("value2");
    }

    [Test]
    public async Task StreamCfgProvider_ReloadAsync_IgnoresMalformedLines()
    {
        var streamFactory = () =>
            new MemoryStream(Encoding.UTF8.GetBytes("key1=value1\nmalformed\nkey2=value2\nkey3"));
        var provider = TestCfgFactory.CreateStreamProvider(streamFactory);

        _ = await provider.ReloadAsync();

        var value1 = provider.Snapshot.GetValue("key1");
        var value2 = provider.Snapshot.GetValue("key2");
        var value3 = provider.Snapshot.GetValue("key3");
        var malformedValue = provider.Snapshot.GetValue("malformed");

        await Assert.That(value1).IsEqualTo("value1");
        await Assert.That(value2).IsEqualTo("value2");
        await Assert.That(value3).IsNull();
        await Assert.That(malformedValue).IsNull();
    }

    [Test]
    public async Task StreamCfgProvider_ReloadAsync_TrimsKeyAndValue()
    {
        var streamFactory = () =>
            new MemoryStream(Encoding.UTF8.GetBytes("  key1  =  value1  \n  key2=value2  "));
        var provider = TestCfgFactory.CreateStreamProvider(streamFactory);

        _ = await provider.ReloadAsync();

        var value1 = provider.Snapshot.GetValue("key1");
        var value2 = provider.Snapshot.GetValue("key2");

        await Assert.That(value1).IsEqualTo("value1");
        await Assert.That(value2).IsEqualTo("value2");
    }

    [Test]
    public async Task StreamCfgProvider_GetValue_ReturnsNullForMissingKey()
    {
        var streamFactory = () => new MemoryStream(Encoding.UTF8.GetBytes("key1=value1"));
        var provider = TestCfgFactory.CreateStreamProvider(streamFactory);

        _ = await provider.ReloadAsync();

        var value = provider.Snapshot.GetValue("missing");

        await Assert.That(value).IsNull();
    }

    [Test]
    public async Task StreamCfgProvider_ReloadAsync_ReplacesSnapshotData()
    {
        var currentContent = "key1=oldvalue\nkey2=value2";
        var provider = TestCfgFactory.CreateStreamProvider(
            () => new MemoryStream(Encoding.UTF8.GetBytes(currentContent))
        );

        var initialChanged = await provider.ReloadAsync();

        await Assert.That(initialChanged).IsTrue();
        var oldValue = provider.Snapshot.GetValue("key1");
        await Assert.That(oldValue).IsEqualTo("oldvalue");

        currentContent = "key1=newvalue\nkey3=value3";

        var changed = await provider.ReloadAsync();

        await Assert.That(changed).IsTrue();
        var newValue = provider.Snapshot.GetValue("key1");
        var key2Value = provider.Snapshot.GetValue("key2");
        var key3Value = provider.Snapshot.GetValue("key3");

        await Assert.That(newValue).IsEqualTo("newvalue");
        await Assert.That(key2Value).IsNull();
        await Assert.That(key3Value).IsEqualTo("value3");
    }

    [Test]
    public async Task StreamCfgProvider_Snapshot_IsUpdatedAfterReload()
    {
        var streamFactory = () => new MemoryStream(Encoding.UTF8.GetBytes("key1=value1"));
        var provider = TestCfgFactory.CreateStreamProvider(streamFactory);

        _ = await provider.ReloadAsync();

        await Assert.That(provider.Snapshot.GetValue("key1")).IsEqualTo("value1");
    }

    [Test]
    public async Task StreamCfgProvider_ReloadAsync_PublishesSnapshotAndRetainsStableRead()
    {
        var streamFactory = () => new MemoryStream(Encoding.UTF8.GetBytes("key1=value1"));
        var provider = TestCfgFactory.CreateStreamProvider(streamFactory);

        var changed = await provider.ReloadAsync();

        await Assert.That(changed).IsTrue();
        await Assert.That(provider.Snapshot.GetValue("key1")).IsEqualTo("value1");
    }

    [Test]
    public async Task StreamCfgProvider_ReloadAsync_ChangesSnapshotOnlyWhenDataChanges()
    {
        var currentContent = "key1=value1";
        var provider = TestCfgFactory.CreateStreamProvider(
            () => new MemoryStream(Encoding.UTF8.GetBytes(currentContent))
        );

        var initialChanged = await provider.ReloadAsync();
        var initialSnapshot = provider.Snapshot;

        await Assert.That(initialChanged).IsTrue();

        var unchanged = await provider.ReloadAsync();
        var unchangedSnapshot = provider.Snapshot;
        await Assert.That(unchanged).IsFalse();
        await Assert.That(unchangedSnapshot).IsSameReferenceAs(initialSnapshot);

        currentContent = "key1=value2";
        var changed = await provider.ReloadAsync();
        await Assert.That(changed).IsTrue();
        await Assert.That(provider.Snapshot).IsNotSameReferenceAs(initialSnapshot);
        await Assert.That(provider.Snapshot.GetValue("key1")).IsEqualTo("value2");
    }

    [Test]
    public async Task StreamCfgProvider_ReloadAsync_WhenFactoryReturnsNull_ThrowsInvalidOperationException()
    {
        var provider = TestCfgFactory.CreateStreamProvider(() => null!);

        await Assert
            .That(async () => await provider.ReloadAsync())
            .Throws<InvalidOperationException>();
    }

    [Test]
    public async Task StreamCfgProvider_ReloadAsync_WithVersionStampUnchanged_SkipsStreamFactory()
    {
        var calls = 0;
        var content = "key1=value1";
        var provider = TestCfgFactory.CreateStreamProvider(
            () =>
            {
                calls++;
                return new MemoryStream(Encoding.UTF8.GetBytes(content));
            },
            () => 1
        );

        var initialChanged = await provider.ReloadAsync();
        content = "key1=value2";
        var changed = await provider.ReloadAsync();

        await Assert.That(initialChanged).IsTrue();
        await Assert.That(changed).IsFalse();
        await Assert.That(calls).IsEqualTo(1);
        await Assert.That(provider.Snapshot.GetValue("key1")).IsEqualTo("value1");
    }

    [Test]
    public async Task StreamCfgProvider_ReloadAsync_WithChangedVersionStampAndSameContent_KeepsSnapshot()
    {
        var stamp = 1;
        const string content = "key1=value1";
        var provider = TestCfgFactory.CreateStreamProvider(
            () => new MemoryStream(Encoding.UTF8.GetBytes(content)),
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
    public async Task StreamCfgProvider_ReloadAsync_WithAcceptedNullStamp_TreatsRepeatedNullAsAuthoritativeShortcut()
    {
        var calls = 0;
        var content = "key1=value1";
        var provider = TestCfgFactory.CreateStreamProvider(
            () =>
            {
                calls++;
                return new MemoryStream(Encoding.UTF8.GetBytes(content));
            },
            () => null
        );

        var initialChanged = await provider.ReloadAsync();
        content = "key1=value2";
        var changed = await provider.ReloadAsync();

        await Assert.That(initialChanged).IsTrue();
        await Assert.That(changed).IsFalse();
        await Assert.That(calls).IsEqualTo(1);
        await Assert.That(provider.Snapshot.GetValue("key1")).IsEqualTo("value1");
    }

    [Test]
    public async Task StreamCfgProvider_ReloadAsync_WithChangedStampAndSameContent_UpdatesAuthorityBaseline()
    {
        var calls = 0;
        var stamp = 1;
        var content = "key1=value1";
        var provider = TestCfgFactory.CreateStreamProvider(
            () =>
            {
                calls++;
                return new MemoryStream(Encoding.UTF8.GetBytes(content));
            },
            () => stamp
        );

        var initialChanged = await provider.ReloadAsync();
        var originalSnapshot = provider.Snapshot;
        stamp = 2;
        var unchanged = await provider.ReloadAsync();
        content = "key1=value2";
        var laterShortcut = await provider.ReloadAsync();

        await Assert.That(initialChanged).IsTrue();
        await Assert.That(unchanged).IsFalse();
        await Assert.That(laterShortcut).IsFalse();
        await Assert.That(calls).IsEqualTo(2);
        await Assert.That(provider.Snapshot).IsSameReferenceAs(originalSnapshot);
        await Assert.That(provider.Snapshot.GetValue("key1")).IsEqualTo("value1");
    }

    [Test]
    public async Task StreamCfgProvider_ReloadAsync_WithChangedVersionStampAndChangedContent_PublishesNewSnapshot()
    {
        var stamp = 1;
        var content = "key1=value1";
        var provider = TestCfgFactory.CreateStreamProvider(
            () => new MemoryStream(Encoding.UTF8.GetBytes(content)),
            () => stamp
        );

        var initialChanged = await provider.ReloadAsync();
        var originalSnapshot = provider.Snapshot;
        stamp = 2;
        content = "key1=value2";
        var changed = await provider.ReloadAsync();

        await Assert.That(initialChanged).IsTrue();
        await Assert.That(changed).IsTrue();
        await Assert.That(provider.Snapshot).IsNotSameReferenceAs(originalSnapshot);
        await Assert.That(provider.Snapshot.GetValue("key1")).IsEqualTo("value2");
    }

    [Test]
    public async Task StreamCfgProvider_ReloadAsync_WithDuplicateKeysAndSameVisibleState_KeepsSnapshot()
    {
        var content = "key=first\nkey=value";
        var provider = TestCfgFactory.CreateStreamProvider(
            () => new MemoryStream(Encoding.UTF8.GetBytes(content))
        );

        var initialChanged = await provider.ReloadAsync();
        var originalSnapshot = provider.Snapshot;
        content = "key=different\nkey=value";

        var changed = await provider.ReloadAsync();

        await Assert.That(initialChanged).IsTrue();
        await Assert.That(changed).IsFalse();
        await Assert.That(provider.Snapshot).IsSameReferenceAs(originalSnapshot);
        await Assert.That(provider.Snapshot.GetValue("key")).IsEqualTo("value");
    }

    [Test]
    public async Task StreamCfgProvider_ReloadAsync_CallsVersionStampFactoryOutsideLock()
    {
        StreamCfgProvider? provider = null;
        var versionStampEntered = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously
        );
        var allowVersionStampToExit = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously
        );
        var snapshotReadCompleted = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously
        );
        provider = TestCfgFactory.CreateStreamProvider(
            () => new MemoryStream(Encoding.UTF8.GetBytes("key=value")),
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
    public async Task StreamCfgProvider_ReloadAsync_WithPreCancelledToken_DoesNotInvokeFactories()
    {
        var streamFactoryCalls = 0;
        var versionStampCalls = 0;
        var provider = TestCfgFactory.CreateStreamProvider(
            () =>
            {
                streamFactoryCalls++;
                return new MemoryStream(Encoding.UTF8.GetBytes("key=value"));
            },
            () =>
            {
                versionStampCalls++;
                return 1;
            }
        );

        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert
            .That(async () => await provider.ReloadAsync(cts.Token))
            .Throws<OperationCanceledException>();
        await Assert.That(streamFactoryCalls).IsEqualTo(0);
        await Assert.That(versionStampCalls).IsEqualTo(0);
    }

    [Test]
    public async Task StreamCfgProvider_ReloadAsync_CancellationAfterVersionStamp_DoesNotInvokeStreamFactory()
    {
        var streamFactoryCalls = 0;
        CancellationTokenSource? cancellationSource = null;
        var provider = TestCfgFactory.CreateStreamProvider(
            () =>
            {
                streamFactoryCalls++;
                return new MemoryStream(Encoding.UTF8.GetBytes("key=value"));
            },
            () =>
            {
                cancellationSource!.Cancel();
                return 1;
            }
        );

        using var cts = new CancellationTokenSource();
        cancellationSource = cts;

        await Assert
            .That(async () => await provider.ReloadAsync(cts.Token))
            .Throws<OperationCanceledException>();
        await Assert.That(streamFactoryCalls).IsEqualTo(0);
    }

    [Test]
    public async Task StreamCfgProvider_ReloadAsync_PreservesTextAfterFirstSeparator()
    {
        var provider = TestCfgFactory.CreateStreamProvider(
            () => new MemoryStream(Encoding.UTF8.GetBytes("key=a=b=c"))
        );

        var changed = await provider.ReloadAsync();

        await Assert.That(changed).IsTrue();
        await Assert.That(provider.Snapshot.GetValue("key")).IsEqualTo("a=b=c");
    }

    [Test]
    public async Task DictionarySource_PreservesValuesWithoutTextRoundTrip()
    {
        var builder = Cfg.CreateBuilder();
        builder.Add(
            new Dictionary<string, string>
            {
                ["withEquals"] = "a=b=c",
                ["withNewLine"] = "line1\nline2",
            }
        );

        var config = await builder.BuildAsync();

        await Assert.That(config.GetValue("withEquals")).IsEqualTo("a=b=c");
        await Assert.That(config.GetValue("withNewLine")).IsEqualTo("line1\nline2");
    }

    [Test]
    public async Task BuilderAdd_StreamFactoryWithVersionStamp_UsesVersionStampShortCircuit()
    {
        var builder = Cfg.CreateBuilder();
        var content = "key=value1";
        var stamp = 1;
        var calls = 0;

        builder.Add(
            streamFactory: () =>
            {
                calls++;
                return new MemoryStream(Encoding.UTF8.GetBytes(content));
            },
            versionStampFactory: () => stamp
        );

        var root = await builder.BuildAsync();
        stamp = 1;
        content = "key=value2";
        var changed = await root.ReloadAsync();

        await Assert.That(changed).IsFalse();
        await Assert.That(calls).IsEqualTo(1);
        await Assert.That(root.GetValue("key")).IsEqualTo("value1");
    }
}
