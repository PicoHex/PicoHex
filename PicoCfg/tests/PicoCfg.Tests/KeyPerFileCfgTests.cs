namespace PicoCfg.Tests;

public class KeyPerFileCfgTests
{
    private static string CreateTempDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"PicoCfg_KPF_{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        return dir;
    }

    [Test]
    public async Task AddKeyPerFile_Basic_FileNameIsKey()
    {
        var dir = CreateTempDir();
        try
        {
            File.WriteAllText(Path.Combine(dir, "name"), "PicoCfg", Encoding.UTF8);
            File.WriteAllText(Path.Combine(dir, "port"), "8080", Encoding.UTF8);

            await using var root = await Cfg.CreateBuilder()
                .AddKeyPerFile(dir)
                .BuildAsync();

            await root.ReloadAsync();

            await Assert.That(root.GetValue("name")).IsEqualTo("PicoCfg");
            await Assert.That(root.GetValue("port")).IsEqualTo("8080");
        }
        finally
        {
            try { Directory.Delete(dir, true); } catch { }
        }
    }

    [Test]
    public async Task AddKeyPerFile_DefaultFilter_SkipsHiddenFiles()
    {
        var dir = CreateTempDir();
        try
        {
            File.WriteAllText(Path.Combine(dir, "visible"), "yes", Encoding.UTF8);
            File.WriteAllText(Path.Combine(dir, ".hidden"), "no", Encoding.UTF8);

            await using var root = await Cfg.CreateBuilder()
                .AddKeyPerFile(dir)
                .BuildAsync();

            await root.ReloadAsync();

            await Assert.That(root.GetValue("visible")).IsEqualTo("yes");
            await Assert.That(root.GetValue(".hidden")).IsNull();
        }
        finally
        {
            try { Directory.Delete(dir, true); } catch { }
        }
    }

    [Test]
    public async Task AddKeyPerFile_EmptyDirectory_ReturnsEmptyConfig()
    {
        var dir = CreateTempDir();
        try
        {
            await using var root = await Cfg.CreateBuilder()
                .AddKeyPerFile(dir)
                .BuildAsync();

            await root.ReloadAsync();

            var all = root.GetAll();
            await Assert.That(all.Count).IsEqualTo(0);
        }
        finally
        {
            try { Directory.Delete(dir, true); } catch { }
        }
    }

    [Test]
    public async Task AddKeyPerFile_NonExistentDirectory_ReturnsEmptyConfig()
    {
        var nonExistentPath = Path.Combine(Path.GetTempPath(), $"PicoCfg_NonExist_{Guid.NewGuid():N}");

        await using var root = await Cfg.CreateBuilder()
            .AddKeyPerFile(nonExistentPath)
            .BuildAsync();

        await root.ReloadAsync();

        var all = root.GetAll();
        await Assert.That(all.Count).IsEqualTo(0);
    }

    [Test]
    public async Task AddKeyPerFile_CustomFilter_SkipsByPredicate()
    {
        var dir = CreateTempDir();
        try
        {
            File.WriteAllText(Path.Combine(dir, "a.txt"), "text content", Encoding.UTF8);
            File.WriteAllText(Path.Combine(dir, "b.cfg"), "cfg content", Encoding.UTF8);

            await using var root = await Cfg.CreateBuilder()
                .AddKeyPerFile(dir, keyFilter: f => f.EndsWith(".cfg"))
                .BuildAsync();

            await root.ReloadAsync();

            await Assert.That(root.GetValue("b.cfg")).IsEqualTo("cfg content");
            await Assert.That(root.GetValue("a.txt")).IsNull();
        }
        finally
        {
            try { Directory.Delete(dir, true); } catch { }
        }
    }

    [Test]
    public async Task AddKeyPerFile_TrailingNewline_Trimmed()
    {
        var dir = CreateTempDir();
        try
        {
            File.WriteAllText(Path.Combine(dir, "key"), "value\n", Encoding.UTF8);

            await using var root = await Cfg.CreateBuilder()
                .AddKeyPerFile(dir)
                .BuildAsync();

            await root.ReloadAsync();

            await Assert.That(root.GetValue("key")).IsEqualTo("value");
        }
        finally
        {
            try { Directory.Delete(dir, true); } catch { }
        }
    }

    [Test]
    public async Task AddKeyPerFile_CancellationTokenInLoop()
    {
        var dir = CreateTempDir();
        try
        {
            // Create many files so that enumeration is non-trivial.
            for (var i = 0; i < 100; i++)
                File.WriteAllText(Path.Combine(dir, $"file_{i:D4}"), $"value_{i}", Encoding.UTF8);

            await using var root = await Cfg.CreateBuilder()
                .AddKeyPerFile(dir)
                .BuildAsync();

            using var cts = new CancellationTokenSource();
            await cts.CancelAsync();

            await Assert.That(async () => await root.ReloadAsync(cts.Token))
                .Throws<OperationCanceledException>();
        }
        finally
        {
            try { Directory.Delete(dir, true); } catch { }
        }
    }
}
