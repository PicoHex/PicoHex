namespace PicoLog.Tests;

public sealed class GeneratorIntegrationTests
{
    [Test]
    public async Task GeneratedLogging_WithEventId_PassesEventId()
    {
        var sink = new CollectingSink();
        await using var factory = new LoggerFactory([sink]);
        var logger = factory.CreateLogger("Tests.Generated");

        logger.LogUserCreated(42, "Alice");
        await factory.DisposeAsync();

        var entry = sink.Entries.Single();
        await Assert.That(entry.EventId.Id).IsEqualTo(1001);
        await Assert.That(entry.EventId.Name).IsEqualTo("UserCreated");
        await Assert.That(entry.Message).IsEqualTo("User 42 (Alice) created");
        await Assert.That(entry.Level).IsEqualTo(LogLevel.Info);
    }

    [Test]
    public async Task GeneratedLogging_WithoutEventId_HasEventIdZero()
    {
        var sink = new CollectingSink();
        await using var factory = new LoggerFactory([sink]);
        var logger = factory.CreateLogger("Tests.Generated");

        logger.LogDiskLow(500);
        await factory.DisposeAsync();

        var entry = sink.Entries.Single();
        await Assert.That(entry.EventId.Id).IsEqualTo(0);
        await Assert.That(entry.EventId.Name).IsNull();
        await Assert.That(entry.Message).IsEqualTo("Disk space low: 500 MB");
        await Assert.That(entry.Level).IsEqualTo(LogLevel.Warning);
    }

    [Test]
    public async Task GeneratedLogging_MultipleCalls_EachProducesEntry()
    {
        var sink = new CollectingSink();
        await using var factory = new LoggerFactory([sink]);
        var logger = factory.CreateLogger("Tests.Generated");

        logger.LogUserCreated(1, "Bob");
        logger.LogUserCreated(2, "Charlie");
        logger.LogDiskLow(100);
        await factory.DisposeAsync();

        var entries = sink.Entries.ToArray();
        await Assert.That(entries).Count().IsEqualTo(3);
        await Assert.That(entries[0].Message).IsEqualTo("User 1 (Bob) created");
        await Assert.That(entries[1].Message).IsEqualTo("User 2 (Charlie) created");
        await Assert.That(entries[2].Message).IsEqualTo("Disk space low: 100 MB");
    }

    private sealed class CollectingSink : ILogSink
    {
        private readonly ConcurrentQueue<LogEntry> _entries = [];

        public IReadOnlyCollection<LogEntry> Entries => _entries.ToArray();

        public Task WriteAsync(LogEntry entry, CancellationToken cancellationToken = default)
        {
            _entries.Enqueue(entry);
            return Task.CompletedTask;
        }

        public void Dispose() { }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
