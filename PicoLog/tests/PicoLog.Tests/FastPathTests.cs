namespace PicoLog.Tests;

public sealed class FastPathTests
{
    private sealed class FastSink : IFastLogSink
    {
        private readonly ConcurrentQueue<LogEntry> _entries = new();
        private int _writeCallCount;

        public IReadOnlyCollection<LogEntry> Entries => _entries.ToArray();
        public int WriteCallCount => Volatile.Read(ref _writeCallCount);

        public Task WriteAsync(LogEntry entry, CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref _writeCallCount);
            _entries.Enqueue(entry);
            return Task.CompletedTask;
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    [Test]
    public async Task FastPath_WritesDirectlyToSink()
    {
        var sink = new FastSink();
        await using var factory = new LoggerFactory(
            [sink],
            new LoggerFactoryOptions { MinLevel = LogLevel.Trace }
        );
        var logger = factory.CreateLogger("Test");

        logger.Info("hello");

        // Fast path: entries should be written synchronously without going through queue
        // No need to flush — entry should already be in the sink
        await Assert.That(sink.WriteCallCount).IsEqualTo(1);
        await Assert.That(sink.Entries.Single().Message).IsEqualTo("hello");
    }
}
