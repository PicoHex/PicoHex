namespace PicoCfg.Ini.Tests;

public class CfgBuilderIniExtensionsTests
{
    [Test]
    public async Task AddIni_SimpleSection_FlattensCorrectly()
    {
        var builder = Cfg.CreateBuilder();
        builder.AddIni("[App]\nName=test\nValue=42");

        var root = await builder.BuildAsync();

        await Assert.That(root.GetValue("App:Name")).IsEqualTo("test");
        await Assert.That(root.GetValue("App:Value")).IsEqualTo("42");
    }

    [Test]
    public async Task AddIni_NestedSections_UsesColonSeparator()
    {
        var builder = Cfg.CreateBuilder();
        builder.AddIni("[A]\nB=v\n[A.C]\nD=w");

        var root = await builder.BuildAsync();

        await Assert.That(root.GetValue("A:B")).IsEqualTo("v");
        await Assert.That(root.GetValue("A:C:D")).IsEqualTo("w");
    }

    [Test]
    public async Task AddIni_LastSourceOverridesEarlier()
    {
        var builder = Cfg.CreateBuilder();
        builder.AddIni("[Section]\nKey=first");
        builder.AddIni("[Section]\nKey=second");

        var root = await builder.BuildAsync();

        await Assert.That(root.GetValue("Section:Key")).IsEqualTo("second");
    }
}
