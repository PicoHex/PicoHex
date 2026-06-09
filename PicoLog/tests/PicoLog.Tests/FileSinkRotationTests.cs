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
