namespace PicoCfg.Tests;

public class FileWatchingCfgTests
{
    [Test]
    public async Task FileWatchingCfgProvider_ReloadDelegate_WorksWithoutWatcher()
    {
        var tempPath = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(tempPath, "key=value1", Encoding.UTF8);

            await using var root = await Cfg.CreateBuilder()
                .Add(() => File.OpenRead(tempPath), watchPath: tempPath)
                .BuildAsync();

            // Data is already loaded during BuildAsync (OpenAsync calls ReloadAsync internally).
            await Assert.That(root.GetValue("key")).IsEqualTo("value1");

            // Second reload with unchanged content delegates to inner and returns false.
            var unchanged = await root.ReloadAsync();
            await Assert.That(unchanged).IsFalse();
            await Assert.That(root.GetValue("key")).IsEqualTo("value1");
        }
        finally
        {
            try { File.Delete(tempPath); } catch { /* best-effort cleanup */ }
        }
    }

    [Test]
    public async Task FileWatchingCfgProvider_Dispose_DisposesInnerAndWatcher()
    {
        var tempPath = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(tempPath, "key=value", Encoding.UTF8);

            await using var root = await Cfg.CreateBuilder()
                .Add(() => File.OpenRead(tempPath), watchPath: tempPath)
                .BuildAsync();

            // Dispose via await using — inner provider and watcher disposed cleanly.
            // If dispose throws, the test fails naturally.
        }
        finally
        {
            try { File.Delete(tempPath); } catch { /* best-effort cleanup */ }
        }
    }

    [Test]
    public async Task FileWatchingCfgProvider_FileChange_TriggersReload()
    {
        var tempPath = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(tempPath, "key=value1", Encoding.UTF8);

            await using var root = await Cfg.CreateBuilder()
                .Add(() => File.OpenRead(tempPath), watchPath: tempPath)
                .BuildAsync();

            // Data loaded during BuildAsync via OpenAsync → ReloadAsync.
            await Assert.That(root.GetValue("key")).IsEqualTo("value1");

            await File.WriteAllTextAsync(tempPath, "key=value2", Encoding.UTF8);

            // Wait for FileSystemWatcher to fire + debounce (200ms default) + inner provider reload.
            await Task.Delay(1500);

            // Reload the root so it re-samples all provider snapshots and composes a new root snapshot.
            var changed = await root.ReloadAsync();
            await Assert.That(changed).IsTrue();
            await Assert.That(root.GetValue("key")).IsEqualTo("value2");
        }
        finally
        {
            try { File.Delete(tempPath); } catch { /* best-effort cleanup */ }
        }
    }
}
