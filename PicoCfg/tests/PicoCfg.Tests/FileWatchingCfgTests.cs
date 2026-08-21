namespace PicoCfg.Tests;

using System.Collections.Concurrent;

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
                .Add(
                    ct => ValueTask.FromResult<Stream>(File.OpenRead(tempPath)),
                    watchPath: tempPath
                )
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
            try
            {
                File.Delete(tempPath);
            }
            catch
            { /* best-effort cleanup */
            }
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
                .Add(
                    ct => ValueTask.FromResult<Stream>(File.OpenRead(tempPath)),
                    watchPath: tempPath
                )
                .BuildAsync();

            // Dispose via await using — inner provider and watcher disposed cleanly.
            // If dispose throws, the test fails naturally.
        }
        finally
        {
            try
            {
                File.Delete(tempPath);
            }
            catch
            { /* best-effort cleanup */
            }
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
                .Add(
                    ct => ValueTask.FromResult<Stream>(File.OpenRead(tempPath)),
                    watchPath: tempPath
                )
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
            try
            {
                File.Delete(tempPath);
            }
            catch
            { /* best-effort cleanup */
            }
        }
    }

    [Test]
    public async Task DisposeAsync_DuringPendingReload_CancelsCleanly()
    {
        var tempPath = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(tempPath, "key=value1", Encoding.UTF8);

            var root = await Cfg.CreateBuilder()
                .Add(
                    ct => ValueTask.FromResult<Stream>(File.OpenRead(tempPath)),
                    watchPath: tempPath
                )
                .BuildAsync();

            await Assert.That(root.GetValue("key")).IsEqualTo("value1");

            // Trigger a file change.
            await File.WriteAllTextAsync(tempPath, "key=value2", Encoding.UTF8);

            await Task.Delay(50);

            await root.DisposeAsync();
        }
        finally
        {
            try
            {
                File.Delete(tempPath);
            }
            catch
            { /* best-effort cleanup */
            }
        }
    }

    [Test]
    public async Task FileWatchingCfgProvider_DirectoryDeleted_ReportsErrorAndRecovers()
    {
        // BUG-REPORT regression: deleting the watched directory made FileSystemWatcher
        // raise its Error event; the provider then re-created the watcher against the
        // missing directory, throwing an unhandled ArgumentException on a thread-pool
        // thread and crashing the whole process. The watcher must instead report via
        // OnFileWatchError and retry until the directory exists again.
        var tempDir = Path.Combine(
            Path.GetTempPath(),
            "picocfg-fw-" + Guid.NewGuid().ToString("N")
        );
        var tempPath = Path.Combine(tempDir, "settings.txt");
        Directory.CreateDirectory(tempDir);
        try
        {
            await File.WriteAllTextAsync(tempPath, "key=value1", Encoding.UTF8);

            var watchErrors = new ConcurrentQueue<(string Context, Exception Error)>();
            var builder = Cfg.CreateBuilder();
            builder.OnFileWatchError = (context, ex) => watchErrors.Enqueue((context, ex));

            await using var root = await builder
                .Add(
                    ct => ValueTask.FromResult<Stream>(File.OpenRead(tempPath)),
                    watchPath: tempPath
                )
                .BuildAsync();

            await Assert.That(root.GetValue("key")).IsEqualTo("value1");

            // Delete the whole watched directory.
            // Windows: FileSystemWatcher raises its Error event here — the provider must
            // report via OnFileWatchError instead of crashing (before the fix this threw
            // an unhandled ArgumentException on a thread-pool thread and killed the host).
            // Linux/macOS: inotify does not raise an Error event for a deleted watch —
            // the callback assertion is therefore Windows-only; the no-crash + recovery
            // assertions apply on every platform.
            Directory.Delete(tempDir, recursive: true);

            if (OperatingSystem.IsWindows())
            {
                // The "watch" context proves TryStartWatcher's missing-directory path
                // surfaced through the user callback instead of throwing.
                await WaitUntilAsync(
                    () => watchErrors.Any(static e => e.Context == "watch"),
                    TimeSpan.FromSeconds(10)
                );
                await Assert.That(watchErrors).IsNotEmpty();
            }
            else
            {
                // Give any platform-specific events time to be processed (validates no crash).
                await Task.Delay(1500);
            }

            // Recreate the directory + file — the provider must recover and keep working.
            Directory.CreateDirectory(tempDir);
            await File.WriteAllTextAsync(tempPath, "key=value2", Encoding.UTF8);

            // Allow the retry timer to re-attach the watcher, then reload.
            await Task.Delay(2500);
            var changed = await root.ReloadAsync();
            await Assert.That(changed).IsTrue();
            await Assert.That(root.GetValue("key")).IsEqualTo("value2");
        }
        finally
        {
            try
            {
                if (Directory.Exists(tempDir))
                    Directory.Delete(tempDir, recursive: true);
            }
            catch
            { /* best-effort cleanup */
            }
        }
    }

    private static async Task WaitUntilAsync(Func<bool> condition, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (!condition())
        {
            if (DateTime.UtcNow >= deadline)
                throw new TimeoutException(
                    $"Condition was not met within {timeout} ({nameof(FileWatchingCfgTests)})."
                );
            await Task.Delay(100);
        }
    }
}
