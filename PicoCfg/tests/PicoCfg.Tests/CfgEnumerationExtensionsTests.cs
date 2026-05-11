namespace PicoCfg.Tests;

public class CfgEnumerationExtensionsTests
{
    [Test]
    public async Task GetAll_WithSingleSource_ReturnsAllValues()
    {
        await using var root = await Cfg
            .CreateBuilder()
            .Add(new Dictionary<string, string>
            {
                ["key1"] = "value1",
                ["key2"] = "value2",
            })
            .BuildAsync();

        var all = root.GetAll();

        await Assert.That(all.Count).IsEqualTo(2);
        await Assert.That(all["key1"]).IsEqualTo("value1");
        await Assert.That(all["key2"]).IsEqualTo("value2");
    }

    [Test]
    public async Task GetAll_WithMultipleSources_LaterOverridesEarlier()
    {
        await using var root = await Cfg
            .CreateBuilder()
            .Add(new Dictionary<string, string>
            {
                ["shared"] = "first",
                ["unique1"] = "val1",
            })
            .Add(new Dictionary<string, string>
            {
                ["shared"] = "second",
                ["unique2"] = "val2",
            })
            .BuildAsync();

        var all = root.GetAll();

        await Assert.That(all.Count).IsEqualTo(3);
        await Assert.That(all["shared"]).IsEqualTo("second");
        await Assert.That(all["unique1"]).IsEqualTo("val1");
        await Assert.That(all["unique2"]).IsEqualTo("val2");
    }

    [Test]
    public async Task GetAll_WithExternalCfg_ReturnsEmpty()
    {
        var external = new ExternalCfg();
        var all = external.GetAll();

        await Assert.That(all.Count).IsEqualTo(0);
    }

    private sealed class ExternalCfg : ICfg
    {
        public bool TryGetValue(string path, out string? value)
        {
            value = null;
            return false;
        }
    }
}
