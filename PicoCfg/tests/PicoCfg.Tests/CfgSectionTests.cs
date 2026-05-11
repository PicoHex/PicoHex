namespace PicoCfg.Tests;

public class CfgSectionTests
{
    [Test]
    public async Task GetSection_ReturnsScopedView()
    {
        await using var root = await Cfg
            .CreateBuilder()
            .Add(new Dictionary<string, string>
            {
                ["Logging:Level"] = "Debug",
                ["App:Name"] = "Pico",
            })
            .BuildAsync();

        var loggingSection = root.GetSection("Logging");

        await Assert.That(loggingSection.GetValue("Level")).IsEqualTo("Debug");
        await Assert.That(loggingSection.GetValue("App:Name")).IsNull();
    }

    [Test]
    public async Task GetSection_Nested_ComposesCorrectly()
    {
        await using var root = await Cfg
            .CreateBuilder()
            .Add(new Dictionary<string, string> { ["A:B:C"] = "deep" })
            .BuildAsync();

        var value = root.GetSection("A").GetSection("B").GetValue("C");

        await Assert.That(value).IsEqualTo("deep");
    }

    [Test]
    public async Task GetSection_LiveView_ReflectsReload()
    {
        var stamp = 1;
        var value = "before";
        var builder = Cfg.CreateBuilder();
        builder.Add(() => new Dictionary<string, string> { ["Logging:Level"] = value }, () => stamp);
        await using var root = await builder.BuildAsync();
        var section = root.GetSection("Logging");

        await Assert.That(section.GetValue("Level")).IsEqualTo("before");

        value = "after";
        stamp = 2;
        var changed = await root.ReloadAsync();

        await Assert.That(changed).IsTrue();
        await Assert.That(section.GetValue("Level")).IsEqualTo("after");
    }

    [Test]
    public async Task GetSection_EmptyPath_IsIdentity()
    {
        await using var root = await Cfg
            .CreateBuilder()
            .Add(new Dictionary<string, string> { ["Key"] = "value" })
            .BuildAsync();

        var emptySection = root.GetSection("");
        var nullSection = root.GetSection(null!);

        await Assert.That(emptySection.GetValue("Key")).IsEqualTo("value");
        await Assert.That(nullSection.GetValue("Key")).IsEqualTo("value");
        await Assert.That(emptySection.GetValue("missing")).IsNull();
        await Assert.That(nullSection.GetValue("missing")).IsNull();
    }
}
