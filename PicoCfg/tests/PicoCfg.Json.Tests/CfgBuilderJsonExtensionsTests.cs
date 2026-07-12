namespace PicoCfg.Json.Tests;

public class CfgBuilderJsonExtensionsTests
{
    [Test]
    public async Task AddJson_SimpleObject_FlattensCorrectly()
    {
        var builder = Cfg.CreateBuilder();
        builder.AddJson("""{"Name": "test", "Value": "42"}""");

        var root = await builder.BuildAsync();

        await Assert.That(root.GetValue("Name")).IsEqualTo("test");
        await Assert.That(root.GetValue("Value")).IsEqualTo("42");
    }

    [Test]
    public async Task AddJson_NestedObject_UsesColonSeparator()
    {
        var builder = Cfg.CreateBuilder();
        builder.AddJson("""{"A": {"B": "v", "C": {"D": "w"}}}""");

        var root = await builder.BuildAsync();

        await Assert.That(root.GetValue("A:B")).IsEqualTo("v");
        await Assert.That(root.GetValue("A:C:D")).IsEqualTo("w");
    }

    [Test]
    public async Task AddJson_WithArrays_SkipsArrays()
    {
        var builder = Cfg.CreateBuilder();
        builder.AddJson("""{"Items": [1, 2, 3], "Name": "t"}""");

        var root = await builder.BuildAsync();

        await Assert.That(root.GetValue("Name")).IsEqualTo("t");
        // Array keys are silently skipped — no key is created for Items
        await Assert.That(root.GetValue("Items")).IsNull();
    }

    [Test]
    public async Task AddJson_WithDifferentValueTypes_StoredAsStrings()
    {
        var builder = Cfg.CreateBuilder();
        builder.AddJson("""{"Str": "s", "Num": 42, "Bool": true, "NullVal": null}""");

        var root = await builder.BuildAsync();

        await Assert.That(root.GetValue("Str")).IsEqualTo("s");
        await Assert.That(root.GetValue("Num")).IsEqualTo("42");
        await Assert.That(root.GetValue("Bool")).IsEqualTo("true");
        await Assert.That(root.GetValue("NullVal")).IsNull(); // null becomes missing
    }

    [Test]
    public async Task AddJson_LastSourceOverridesEarlier()
    {
        var builder = Cfg.CreateBuilder();
        builder.AddJson("""{"Key": "first"}""");
        builder.AddJson("""{"Key": "second"}""");

        var root = await builder.BuildAsync();

        await Assert.That(root.GetValue("Key")).IsEqualTo("second");
    }

    [Test]
    public async Task GetAll_WithJsonSource_ReturnsAllFlattenedKeys()
    {
        var builder = Cfg.CreateBuilder();
        builder.AddJson("""{"model": "deepseek-v4-flash", "timeout": 30}""");

        var root = await builder.BuildAsync();

        var all = root.GetAll();

        await Assert.That(all.Count).IsEqualTo(2);
        await Assert.That(all["model"]).IsEqualTo("deepseek-v4-flash");
        await Assert.That(all["timeout"]).IsEqualTo("30");
    }
}
