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

    [Test]
    public async Task AddIniFile_FileChanged_ReloadPublishesNewValue()
    {
        var path = Path.Combine(Path.GetTempPath(), $"picocfg-ini-{Guid.NewGuid():N}.ini");
        await File.WriteAllTextAsync(path, "Name=first");

        ICfgRoot? root = null;
        try
        {
            var builder = Cfg.CreateBuilder();
            builder.AddIniFile(path);
            root = await builder.BuildAsync();

            await Assert.That(root.GetValue("Name")).IsEqualTo("first");

            await File.WriteAllTextAsync(path, "Name=second");

            var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(10);
            while (root.GetValue("Name") != "second" && DateTime.UtcNow < deadline)
            {
                await Task.Delay(100);
                await root.ReloadAsync();
            }

            await Assert.That(root.GetValue("Name")).IsEqualTo("second");
        }
        finally
        {
            if (root is not null)
                await root.DisposeAsync();
            File.Delete(path);
        }
    }
}
