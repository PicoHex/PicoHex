namespace PicoCfg.Tests;

public class CfgSnapshotComposerTests
{
    [Test]
    public async Task CreateSnapshot_WithNoProviderSnapshots_ReturnsCfgSnapshotEmpty()
    {
        var snapshot = CfgSnapshotComposer.CreateSnapshot([], CreateSnapshot);

        await Assert.That(snapshot).IsSameReferenceAs(CfgSnapshot.Empty);
    }

    [Test]
    public async Task CreateSnapshot_WithSingleProviderSnapshot_ReturnsSameSnapshotReference()
    {
        ICfgSnapshot providerSnapshot = new DelegatingSnapshot(
            path => path == "key" ? "value" : null
        );

        var snapshot = CfgSnapshotComposer.CreateSnapshot([providerSnapshot], CreateSnapshot);

        await Assert.That(snapshot).IsSameReferenceAs(providerSnapshot);
    }

    [Test]
    public async Task CreateSnapshot_WithOnlyCfgSnapshots_CallsSnapshotFactoryWithMergedVisibleValues()
    {
        IReadOnlyDictionary<string, string>? mergedValues = null;
        var first = CreateSnapshot(
            new Dictionary<string, string> { ["shared"] = "first", ["first-only"] = "1", },
            10
        );
        var second = CreateSnapshot(
            new Dictionary<string, string> { ["shared"] = "second", ["second-only"] = "2", },
            20
        );

        var snapshot = CfgSnapshotComposer.CreateSnapshot(
            [first, second],
            (values, fingerprint) =>
            {
                mergedValues = new Dictionary<string, string>(values);
                return new CfgSnapshot(values, fingerprint);
            }
        );

        await Assert.That(mergedValues).IsNotNull();
        await Assert.That(mergedValues!["shared"]).IsEqualTo("second");
        await Assert.That(mergedValues["first-only"]).IsEqualTo("1");
        await Assert.That(mergedValues["second-only"]).IsEqualTo("2");
        await Assert.That(GetValue(snapshot, "shared")).IsEqualTo("second");
    }

    [Test]
    public async Task CreateSnapshot_WithOnlyCfgSnapshots_PassesMergedFingerprintToSnapshotFactory()
    {
        IReadOnlyDictionary<string, string>? mergedValues = null;
        int? observedFingerprint = null;
        var first = CreateSnapshot(new Dictionary<string, string> { ["alpha"] = "1" }, 1);
        var second = CreateSnapshot(new Dictionary<string, string> { ["beta"] = "2" }, 2);

        _ = CfgSnapshotComposer.CreateSnapshot(
            [first, second],
            (values, fingerprint) =>
            {
                mergedValues = new Dictionary<string, string>(values);
                observedFingerprint = fingerprint;
                return new CfgSnapshot(values, fingerprint);
            }
        );

        await Assert.That(mergedValues).IsNotNull();
        var expectedFingerprint = ConfigDataComparer.ComputeFingerprint(mergedValues!);
        await Assert.That(observedFingerprint).IsEqualTo(expectedFingerprint);
    }

    [Test]
    public async Task CreateSnapshot_WithNonCfgSnapshot_FallsBackToCompositeLookup()
    {
        ICfgSnapshot first = CreateSnapshot(
            new Dictionary<string, string> { ["shared"] = "first" }
        );
        ICfgSnapshot second = new DelegatingSnapshot(path => path == "shared" ? "second" : null);

        var snapshot = CfgSnapshotComposer.CreateSnapshot([first, second], CreateSnapshot);

        await Assert.That(GetValue(snapshot, "shared")).IsEqualTo("second");
    }

    [Test]
    public async Task CreateSnapshot_WithCompositeFallback_UsesEarlierProviderWhenLaterProviderMisses()
    {
        ICfgSnapshot first = new DelegatingSnapshot(path => path == "first-only" ? "first" : null);
        ICfgSnapshot second = new DelegatingSnapshot(path => path == "shared" ? "second" : null);

        var snapshot = CfgSnapshotComposer.CreateSnapshot([first, second], CreateSnapshot);

        await Assert.That(GetValue(snapshot, "first-only")).IsEqualTo("first");
    }

    [Test]
    public async Task CreateSnapshot_WithCompositeFallback_MissingKeyReturnsMissing()
    {
        ICfgSnapshot first = new DelegatingSnapshot(path => path == "alpha" ? "1" : null);
        ICfgSnapshot second = new DelegatingSnapshot(path => path == "beta" ? "2" : null);

        var snapshot = CfgSnapshotComposer.CreateSnapshot([first, second], CreateSnapshot);

        await Assert.That(GetValue(snapshot, "missing")).IsNull();
    }

    [Test]
    public async Task SequenceEqual_WithDifferentCounts_ReturnsFalse()
    {
        var equal = CfgSnapshotComposer.SequenceEqual(
            [CreateSnapshot(new Dictionary<string, string> { ["alpha"] = "1" })],
            []
        );

        await Assert.That(equal).IsFalse();
    }

    [Test]
    public async Task SequenceEqual_WithSameReferencesInSameOrder_ReturnsTrue()
    {
        ICfgSnapshot first = CreateSnapshot(new Dictionary<string, string> { ["alpha"] = "1" });
        ICfgSnapshot second = CreateSnapshot(new Dictionary<string, string> { ["beta"] = "2" });

        var equal = CfgSnapshotComposer.SequenceEqual([first, second], [first, second]);

        await Assert.That(equal).IsTrue();
    }

    [Test]
    public async Task SequenceEqual_WithEquivalentButDistinctSnapshots_ReturnsFalse()
    {
        ICfgSnapshot first = CreateSnapshot(new Dictionary<string, string> { ["alpha"] = "1" });
        ICfgSnapshot second = CreateSnapshot(new Dictionary<string, string> { ["alpha"] = "1" });

        var equal = CfgSnapshotComposer.SequenceEqual([first], [second]);

        await Assert.That(equal).IsFalse();
    }

    [Test]
    public async Task SequenceEqual_WithSameReferencesInDifferentOrder_ReturnsFalse()
    {
        ICfgSnapshot first = CreateSnapshot(new Dictionary<string, string> { ["alpha"] = "1" });
        ICfgSnapshot second = CreateSnapshot(new Dictionary<string, string> { ["beta"] = "2" });

        var equal = CfgSnapshotComposer.SequenceEqual([first, second], [second, first]);

        await Assert.That(equal).IsFalse();
    }

    private static CfgSnapshot CreateSnapshot(
        IReadOnlyDictionary<string, string> values,
        int fingerprint
    )
    {
        return new CfgSnapshot(values, fingerprint);
    }

    private static CfgSnapshot CreateSnapshot(IReadOnlyDictionary<string, string> values)
    {
        return new CfgSnapshot(values, ConfigDataComparer.ComputeFingerprint(values));
    }

    private static string? GetValue(ICfgSnapshot snapshot, string path)
    {
        return snapshot.TryGetValue(path, out var value) ? value : null;
    }

    private sealed class DelegatingSnapshot(Func<string, string?> resolver) : ICfgSnapshot
    {
        public bool TryGetValue(string path, out string? value)
        {
            value = resolver(path);
            return value is not null;
        }
    }
}
