namespace PicoCfg.Toml.Tests;

public class CfgBuilderTomlExtensionsTests
{
    [Test]
    public async Task AddToml_SimpleTable_FlattensCorrectly()
    {
        var builder = Cfg.CreateBuilder();
        builder.AddToml("[App]\nName = \"test\"\nValue = \"42\"");

        var root = await builder.BuildAsync();

        await Assert.That(root.GetValue("App:Name")).IsEqualTo("test");
        await Assert.That(root.GetValue("App:Value")).IsEqualTo("42");
    }

    [Test]
    public async Task AddToml_NestedTables_UsesColonSeparator()
    {
        var builder = Cfg.CreateBuilder();
        builder.AddToml("[A]\nB = \"v\"\n[A.C]\nD = \"w\"");

        var root = await builder.BuildAsync();

        await Assert.That(root.GetValue("A:B")).IsEqualTo("v");
        await Assert.That(root.GetValue("A:C:D")).IsEqualTo("w");
    }

    [Test]
    public async Task AddToml_WithDifferentValueTypes_StoredAsStrings()
    {
        var builder = Cfg.CreateBuilder();
        builder.AddToml("Str = \"s\"\nNum = 42\nBool = true");

        var root = await builder.BuildAsync();

        await Assert.That(root.GetValue("Str")).IsEqualTo("s");
        await Assert.That(root.GetValue("Num")).IsEqualTo("42");
        await Assert.That(root.GetValue("Bool")).IsEqualTo("true");
    }

    [Test]
    public async Task AddToml_LastSourceOverridesEarlier()
    {
        var builder = Cfg.CreateBuilder();
        builder.AddToml("[Section]\nKey = \"first\"");
        builder.AddToml("[Section]\nKey = \"second\"");

        var root = await builder.BuildAsync();

        await Assert.That(root.GetValue("Section:Key")).IsEqualTo("second");
    }
}
