namespace PicoCfg.Tests;

public sealed class ChainedCfgTests
{
    [Test]
    public async Task AddConfiguration_ChainedKeys_Accessible()
    {
        await using var root1 = await Cfg.CreateBuilder()
            .Add(new Dictionary<string, string> { ["Shared"] = "fromChain" })
            .BuildAsync();

        await using var root2 = await Cfg.CreateBuilder().AddConfiguration(root1).BuildAsync();

        await Assert.That(root2.GetValue("Shared")).IsEqualTo("fromChain");
    }

    [Test]
    public async Task AddConfiguration_LaterSource_OverridesChain()
    {
        await using var root1 = await Cfg.CreateBuilder()
            .Add(new Dictionary<string, string> { ["Override"] = "old" })
            .BuildAsync();

        await using var root2 = await Cfg.CreateBuilder()
            .AddConfiguration(root1)
            .Add(new Dictionary<string, string> { ["Override"] = "new" })
            .BuildAsync();

        await Assert.That(root2.GetValue("Override")).IsEqualTo("new");
    }

    [Test]
    public async Task AddConfiguration_DisposeIsolation_ChainedNotDisposed()
    {
        await using var root1 = await Cfg.CreateBuilder()
            .Add(new Dictionary<string, string> { ["Key"] = "stillHere" })
            .BuildAsync();

        ICfgRoot root2;
        await using (root2 = await Cfg.CreateBuilder().AddConfiguration(root1).BuildAsync())
        {
            await Assert.That(root2.GetValue("Key")).IsEqualTo("stillHere");
        }

        await Assert.That(root1.GetValue("Key")).IsEqualTo("stillHere");
    }

    [Test]
    public async Task AddConfiguration_EmptyChain_Works()
    {
        await using var empty = await Cfg.CreateBuilder().BuildAsync();

        await using var root = await Cfg.CreateBuilder().AddConfiguration(empty).BuildAsync();

        await Assert.That(root.GetValue("Nonexistent")).IsNull();
    }
}
