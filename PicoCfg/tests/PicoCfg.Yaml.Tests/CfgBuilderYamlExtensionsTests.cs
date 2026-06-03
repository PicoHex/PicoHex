namespace PicoCfg.Yaml.Tests;

public class CfgBuilderYamlExtensionsTests
{
    [Test]
    public async Task AddYaml_SimpleMapping_FlattensCorrectly()
    {
        var builder = Cfg.CreateBuilder();
        builder.AddYaml("Name: test\nValue: \"42\"");

        var root = await builder.BuildAsync();

        await Assert.That(root.GetValue("Name")).IsEqualTo("test");
        await Assert.That(root.GetValue("Value")).IsEqualTo("42");
    }

    [Test]
    public async Task AddYaml_NestedMapping_UsesColonSeparator()
    {
        var builder = Cfg.CreateBuilder();
        builder.AddYaml("A:\n  B: v\n  C:\n    D: w");

        var root = await builder.BuildAsync();

        await Assert.That(root.GetValue("A:B")).IsEqualTo("v");
        await Assert.That(root.GetValue("A:C:D")).IsEqualTo("w");
    }

    [Test]
    public async Task AddYaml_WithDifferentValueTypes_StoredAsStrings()
    {
        var builder = Cfg.CreateBuilder();
        builder.AddYaml("Str: s\nNum: 42\nBool: true\nNullVal:");

        var root = await builder.BuildAsync();

        await Assert.That(root.GetValue("Str")).IsEqualTo("s");
        await Assert.That(root.GetValue("Num")).IsEqualTo("42");
        await Assert.That(root.GetValue("Bool")).IsEqualTo("true");
        await Assert.That(root.GetValue("NullVal")).IsNull();
    }

    [Test]
    public async Task AddYaml_LastSourceOverridesEarlier()
    {
        var builder = Cfg.CreateBuilder();
        builder.AddYaml("Key: first");
        builder.AddYaml("Key: second");

        var root = await builder.BuildAsync();

        await Assert.That(root.GetValue("Key")).IsEqualTo("second");
    }
}
