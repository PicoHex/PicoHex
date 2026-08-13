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

    [Test]
    public async Task AddTomlFile_FileChanged_ReloadPublishesNewValue()
    {
        var path = Path.Combine(Path.GetTempPath(), $"picocfg-toml-{Guid.NewGuid():N}.toml");
        await File.WriteAllTextAsync(path, "Name = \"first\"");

        ICfgRoot? root = null;
        try
        {
            var builder = Cfg.CreateBuilder();
            builder.AddTomlFile(path);
            root = await builder.BuildAsync();

            await Assert.That(root.GetValue("Name")).IsEqualTo("first");

            await File.WriteAllTextAsync(path, "Name = \"second\"");

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
