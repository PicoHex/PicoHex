namespace PicoLog.Tests;

public sealed class BatchDispatchTests
{
    private sealed class BatchingTestSink : IBatchingLogSink
    {
        private int _batchCallCount;
        private int _individualCallCount;
        private readonly ConcurrentQueue<LogEntry> _entries = new();

        public int BatchCallCount => Volatile.Read(ref _batchCallCount);
        public int IndividualCallCount => Volatile.Read(ref _individualCallCount);
        public IReadOnlyCollection<LogEntry> Entries => _entries.ToArray();

        public Task WriteAsync(LogEntry entry, CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref _individualCallCount);
            _entries.Enqueue(entry);
            return Task.CompletedTask;
        }

        public ValueTask WriteBatchAsync(
            IReadOnlyList<LogEntry> batch,
            CancellationToken cancellationToken = default
        )
        {
            Interlocked.Increment(ref _batchCallCount);
            foreach (var entry in batch)
                _entries.Enqueue(entry);
            return ValueTask.CompletedTask;
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    [Test]
    public async Task BatchDispatch_SendsMultipleEntriesInOneCall()
    {
        var sink = new BatchingTestSink();
        await using var factory = new LoggerFactory(
            [sink],
            new LoggerFactoryOptions { MinLevel = LogLevel.Trace }
        );
        var logger = factory.CreateLogger("Test");

        // Write multiple entries
        logger.Info("msg1");
        logger.Info("msg2");
        logger.Info("msg3");

        // Flush to ensure all entries are processed
        await factory.FlushAsync();

        await Assert.That(sink.BatchCallCount).IsGreaterThan(0);
        await Assert.That(sink.Entries.Count).IsEqualTo(3);
    }
}
