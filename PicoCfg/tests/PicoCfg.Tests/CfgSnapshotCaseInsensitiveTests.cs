namespace PicoCfg.Tests;

/// <summary>
/// Behavior contract for case-insensitive lookup fallback. The fallback was
/// O(n) over every key on every miss; it now uses a lazily built
/// case-insensitive index while preserving first-inserted-wins semantics for
/// keys that differ only by case.
/// </summary>
public class CfgSnapshotCaseInsensitiveTests
{
    [Test]
    public async Task ExactHit_SkipsCaseInsensitiveFallback()
    {
        var snapshot = (ICfgSnapshot)
            new CfgSnapshot(new Dictionary<string, string> { ["Name"] = "exact" });

        await Assert.That(snapshot.TryGetValue("Name", out var value)).IsTrue();
        await Assert.That(value).IsEqualTo("exact");
    }

    [Test]
    public async Task CaseInsensitiveMiss_FindsValueAfterFirstMiss()
    {
        var snapshot = (ICfgSnapshot)
            new CfgSnapshot(new Dictionary<string, string> { ["Name"] = "camel" });

        // Exact miss triggers the fallback; the value must still be found.
        await Assert.That(snapshot.TryGetValue("NAME", out var value)).IsTrue();
        await Assert.That(value).IsEqualTo("camel");
    }

    [Test]
    public async Task RepeatedCaseVariants_ReturnConsistentValues()
    {
        var snapshot = (ICfgSnapshot)
            new CfgSnapshot(new Dictionary<string, string> { ["Name"] = "camel" });

        for (var i = 0; i < 3; i++)
        {
            await Assert.That(snapshot.TryGetValue("NAME", out var upper)).IsTrue();
            await Assert.That(upper).IsEqualTo("camel");
            await Assert.That(snapshot.TryGetValue("name", out var lower)).IsTrue();
            await Assert.That(lower).IsEqualTo("camel");
            await Assert.That(snapshot.TryGetValue("nAmE", out var mixed)).IsTrue();
            await Assert.That(mixed).IsEqualTo("camel");
        }
    }

    [Test]
    public async Task DuplicateKeysDifferingOnlyByCase_FirstInsertedWins()
    {
        var snapshot = (ICfgSnapshot)
            new CfgSnapshot(
                new Dictionary<string, string> { ["Name"] = "first", ["NAME"] = "second" }
            );

        await Assert.That(snapshot.TryGetValue("name", out var value)).IsTrue();
        await Assert.That(value).IsEqualTo("first");
    }

    [Test]
    public async Task Fallback_DoesNotMutateGetAllValues()
    {
        var snapshot = (ICfgSnapshot)
            new CfgSnapshot(new Dictionary<string, string> { ["Name"] = "camel" });

        var before = snapshot.GetAllValues();
        _ = snapshot.TryGetValue("NAME", out _);

        await Assert.That(snapshot.GetAllValues().Count).IsEqualTo(before.Count);
        await Assert.That(snapshot.GetAllValues()["Name"]).IsEqualTo("camel");
    }
}
