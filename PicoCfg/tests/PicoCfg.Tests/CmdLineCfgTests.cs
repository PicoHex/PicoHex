namespace PicoCfg.Tests;

public class CmdLineCfgTests
{
    [Test]
    public async Task AddCommandLine_EqualsFormat_ParsesCorrectly()
    {
        await using var root = await Cfg
            .CreateBuilder()
            .AddCommandLine(["--Name=PicoCfg", "--Count=42"])
            .BuildAsync();

        await Assert.That(root.GetValue("Name")).IsEqualTo("PicoCfg");
        await Assert.That(root.GetValue("Count")).IsEqualTo("42");
    }

    [Test]
    public async Task AddCommandLine_SpaceFormat_ParsesCorrectly()
    {
        await using var root = await Cfg
            .CreateBuilder()
            .AddCommandLine(["--Name", "PicoCfg", "--Count", "42"])
            .BuildAsync();

        await Assert.That(root.GetValue("Name")).IsEqualTo("PicoCfg");
        await Assert.That(root.GetValue("Count")).IsEqualTo("42");
    }

    [Test]
    public async Task AddCommandLine_Switch_BecomesTrue()
    {
        await using var root = await Cfg
            .CreateBuilder()
            .AddCommandLine(["--Verbose"])
            .BuildAsync();

        await Assert.That(root.GetValue("Verbose")).IsEqualTo("true");
    }

    [Test]
    public async Task AddCommandLine_ShortForm_ParsesCorrectly()
    {
        await using var root = await Cfg
            .CreateBuilder()
            .AddCommandLine(["-n", "PicoCfg"])
            .BuildAsync();

        await Assert.That(root.GetValue("n")).IsEqualTo("PicoCfg");
    }

    [Test]
    public async Task AddCommandLine_WindowsForm_ParsesCorrectly()
    {
        await using var root = await Cfg
            .CreateBuilder()
            .AddCommandLine(["/Port", "8080"])
            .BuildAsync();

        await Assert.That(root.GetValue("Port")).IsEqualTo("8080");
    }

    [Test]
    public async Task AddCommandLine_EqualsInValue_Preserved()
    {
        await using var root = await Cfg
            .CreateBuilder()
            .AddCommandLine(["--Conn=Host=localhost;Port=5432"])
            .BuildAsync();

        await Assert.That(root.GetValue("Conn")).IsEqualTo("Host=localhost;Port=5432");
    }

    [Test]
    public async Task AddCommandLine_Prefix_FiltersCorrectly()
    {
        await using var root = await Cfg
            .CreateBuilder()
            .AddCommandLine(["--App:Name=Pico", "--Db:Host=localhost"], "App:")
            .BuildAsync();

        await Assert.That(root.GetValue("App:Name")).IsEqualTo("Pico");
        await Assert.That(root.GetValue("Db:Host")).IsNull();
    }

    [Test]
    public async Task AddCommandLine_EmptyArgs_ReturnsEmptyConfig()
    {
        await using var root = await Cfg
            .CreateBuilder()
            .AddCommandLine([])
            .BuildAsync();

        await Assert.That(root.GetValue("anything")).IsNull();
    }

    [Test]
    public async Task AddCommandLine_MixedFormats_AllParsedCorrectly()
    {
        await using var root = await Cfg
            .CreateBuilder()
            .AddCommandLine([
                "--Mode=full",
                "--Name",
                "PicoCfg",
                "-v",
                "/Port",
                "8080",
                "--Verbose",
            ])
            .BuildAsync();

        await Assert.That(root.GetValue("Mode")).IsEqualTo("full");
        await Assert.That(root.GetValue("Name")).IsEqualTo("PicoCfg");
        await Assert.That(root.GetValue("v")).IsEqualTo("true");
        await Assert.That(root.GetValue("Port")).IsEqualTo("8080");
        await Assert.That(root.GetValue("Verbose")).IsEqualTo("true");
    }
}
