namespace PicoLog.Tests;

public class FileSinkRotationTests
{
    private static string GetTempFilePath() =>
        Path.Combine(Path.GetTempPath(), $"pico-rotate-{Guid.NewGuid():N}.log");

    [Test]
    public async Task RotationInterval_Zero_DoesNotRotate()
    {
        var filePath = GetTempFilePath();

        var sink = new FileSink(
            new ConsoleFormatter(),
            new FileSinkOptions { FilePath = filePath, RotationInterval = TimeSpan.Zero }
        );

        await sink.WriteAsync(
            new LogEntry
            {
                Timestamp = DateTimeOffset.UtcNow,
                Level = LogLevel.Info,
                Category = "Test",
                Message = "test message",
            }
        );

        // Don't dispose — just check the file was created
        await Assert.That(File.Exists(filePath)).IsTrue();

        // Cleanup: hard-delete after Dispose completes
        await sink.DisposeAsync();
        TryDelete(filePath);
    }

    [Test]
    public async Task FileSink_CreateWithRotationInterval_DoesNotThrow()
    {
        var filePath = GetTempFilePath();

        // Just constructing with RotationInterval should not throw
        var sink = new FileSink(
            new ConsoleFormatter(),
            new FileSinkOptions { FilePath = filePath, RotationInterval = TimeSpan.FromMinutes(30) }
        );

        await sink.DisposeAsync();
        TryDelete(filePath);
    }

    [Test]
    public async Task Rotation_AcrossRestart_DoesNotOverwritePreviousRunFiles()
    {
        var filePath = GetTempFilePath();
        var fileName = Path.GetFileNameWithoutExtension(filePath);
        var directory = Path.GetDirectoryName(filePath)!;

        var options = new FileSinkOptions
        {
            FilePath = filePath,
            MaxFileSizeBytes = 32, // every formatted line exceeds this -> rotate on each message
            BatchSize = 1, // one message per batch -> one rotation per message
            MaxRetainedFiles = 0, // keep all rotated files (default)
        };

        // First process run.
        var firstRun = new FileSink(new ConsoleFormatter(), options);
        await firstRun.WriteAsync(CreateEntry("run-1-A"));
        await firstRun.WriteAsync(CreateEntry("run-1-B"));
        await firstRun.DisposeAsync();

        // Second process run — simulates a restart of the application.
        var secondRun = new FileSink(new ConsoleFormatter(), options);
        await secondRun.WriteAsync(CreateEntry("run-2-C"));
        await secondRun.WriteAsync(CreateEntry("run-2-D"));
        await secondRun.DisposeAsync();

        // Every message from both runs must survive on disk. The bug clobbers
        // the previous run's app.1.log / app.2.log on the first rotations of
        // the new process, silently destroying run-1 data.
        var files = Directory.GetFiles(directory, $"{fileName}.*").ToList();
        var allText = string.Join("\n", files.Select(File.ReadAllText));

        await Assert.That(allText).Contains("run-1-A");
        await Assert.That(allText).Contains("run-1-B");
        await Assert.That(allText).Contains("run-2-C");
        await Assert.That(allText).Contains("run-2-D");

        foreach (var file in files)
            TryDelete(file);
    }

    private static LogEntry CreateEntry(string message) =>
        new()
        {
            Timestamp = DateTimeOffset.UtcNow,
            Level = LogLevel.Info,
            Category = "Test",
            Message = message,
        };

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch
        { /* best-effort cleanup */
        }
    }
}
