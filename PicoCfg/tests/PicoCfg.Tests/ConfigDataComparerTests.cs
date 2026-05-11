namespace PicoCfg.Tests;

public class ConfigDataComparerTests
{
    [Test]
    public async Task ComputeFingerprint_WithEquivalentDataInDifferentEnumerationOrder_ReturnsSameFingerprint()
    {
        IReadOnlyDictionary<string, string> first = new Dictionary<string, string>
        {
            ["alpha"] = "1",
            ["beta"] = "2",
        };
        IReadOnlyDictionary<string, string> second = new Dictionary<string, string>
        {
            ["beta"] = "2",
            ["alpha"] = "1",
        };

        var firstFingerprint = ConfigDataComparer.ComputeFingerprint(first);
        var secondFingerprint = ConfigDataComparer.ComputeFingerprint(second);

        await Assert.That(secondFingerprint).IsEqualTo(firstFingerprint);
    }

    [Test]
    public async Task Equals_WithDifferentCounts_ReturnsFalse()
    {
        IReadOnlyDictionary<string, string> left = new Dictionary<string, string>
        {
            ["alpha"] = "1",
        };
        IReadOnlyDictionary<string, string> right = new Dictionary<string, string>
        {
            ["alpha"] = "1",
            ["beta"] = "2",
        };

        var equal = ConfigDataComparer.Equals(left, right);

        await Assert.That(equal).IsFalse();
    }

    [Test]
    public async Task Equals_WithMissingKey_ReturnsFalse()
    {
        IReadOnlyDictionary<string, string> left = new Dictionary<string, string>
        {
            ["alpha"] = "1",
            ["beta"] = "2",
        };
        IReadOnlyDictionary<string, string> right = new Dictionary<string, string>
        {
            ["alpha"] = "1",
            ["gamma"] = "2",
        };

        var equal = ConfigDataComparer.Equals(left, right);

        await Assert.That(equal).IsFalse();
    }

    [Test]
    public async Task Equals_WithOrdinalValueDifference_ReturnsFalse()
    {
        IReadOnlyDictionary<string, string> left = new Dictionary<string, string>
        {
            ["alpha"] = "Value",
        };
        IReadOnlyDictionary<string, string> right = new Dictionary<string, string>
        {
            ["alpha"] = "value",
        };

        var equal = ConfigDataComparer.Equals(left, right);

        await Assert.That(equal).IsFalse();
    }

    [Test]
    public async Task Equals_WithEquivalentSeparateDictionaries_ReturnsTrue()
    {
        IReadOnlyDictionary<string, string> left = new Dictionary<string, string>
        {
            ["alpha"] = "1",
            ["beta"] = "2",
        };
        IReadOnlyDictionary<string, string> right = new Dictionary<string, string>
        {
            ["alpha"] = "1",
            ["beta"] = "2",
        };

        var equal = ConfigDataComparer.Equals(left, right);

        await Assert.That(equal).IsTrue();
    }

    [Test]
    public async Task EqualsSnapshot_WithDifferentCount_ReturnsFalse()
    {
        var left = CreateSnapshot(new Dictionary<string, string> { ["alpha"] = "1", });
        IReadOnlyDictionary<string, string> right = new Dictionary<string, string>
        {
            ["alpha"] = "1",
            ["beta"] = "2",
        };

        var equal = ConfigDataComparer.Equals(
            left,
            right,
            ConfigDataComparer.ComputeFingerprint(right)
        );

        await Assert.That(equal).IsFalse();
    }

    [Test]
    public async Task EqualsSnapshot_WithDifferentFingerprint_ReturnsFalse()
    {
        IReadOnlyDictionary<string, string> values = new Dictionary<string, string>
        {
            ["alpha"] = "1",
        };
        var left = CreateSnapshot(values);

        var equal = ConfigDataComparer.Equals(left, values, left.Fingerprint + 1);

        await Assert.That(equal).IsFalse();
    }

    [Test]
    public async Task EqualsSnapshot_WithMatchingFingerprintButDifferentValues_ReturnsFalse()
    {
        var left = CreateSnapshot(
            new Dictionary<string, string> { ["alpha"] = "1", ["beta"] = "2", }
        );
        IReadOnlyDictionary<string, string> right = new Dictionary<string, string>
        {
            ["alpha"] = "1",
            ["beta"] = "changed",
        };

        var equal = ConfigDataComparer.Equals(left, right, left.Fingerprint);

        await Assert.That(equal).IsFalse();
    }

    [Test]
    public async Task EqualsSnapshot_WithMatchingFingerprintAndValues_ReturnsTrue()
    {
        IReadOnlyDictionary<string, string> values = new Dictionary<string, string>
        {
            ["alpha"] = "1",
            ["beta"] = "2",
        };
        var left = CreateSnapshot(values);
        IReadOnlyDictionary<string, string> right = new Dictionary<string, string>
        {
            ["alpha"] = "1",
            ["beta"] = "2",
        };

        var equal = ConfigDataComparer.Equals(left, right, left.Fingerprint);

        await Assert.That(equal).IsTrue();
    }

    private static CfgSnapshot CreateSnapshot(IReadOnlyDictionary<string, string> values)
    {
        return new CfgSnapshot(values, ConfigDataComparer.ComputeFingerprint(values));
    }
}
