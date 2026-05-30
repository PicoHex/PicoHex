namespace PicoLog.Tests;

public sealed class LogEntryPoolingIntegrationTests
{
    private sealed class RecordingSink : IFastLogSink, IBatchingLogSink
    {
        private readonly ConcurrentQueue<string> _messages = new();
        private int _batchCount;

        public IReadOnlyCollection<string> Messages => _messages.ToArray();
        public int BatchCallCount => Volatile.Read(ref _batchCount);

        public Task WriteAsync(LogEntry entry, CancellationToken cancellationToken = default)
        {
            // Copy the message — entry may be returned to pool after dispatch
            _messages.Enqueue(entry.Message ?? string.Empty);
            return Task.CompletedTask;
        }

        public ValueTask WriteBatchAsync(
            IReadOnlyList<LogEntry> batch,
            CancellationToken cancellationToken = default
        )
        {
            Interlocked.Increment(ref _batchCount);
            foreach (var entry in batch)
                _messages.Enqueue(entry.Message ?? string.Empty);
            return ValueTask.CompletedTask;
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    [Test]
    public async Task PooledEntries_PreserveCorrectData_AfterBatchDispatch()
    {
        var sink = new RecordingSink();
        await using var factory = new LoggerFactory(
            [sink],
            new LoggerFactoryOptions { MinLevel = LogLevel.Trace }
        );
        var logger = factory.CreateLogger("Test");

        // Write many entries with unique data to stress pooling
        const int count = 200;
        for (var i = 0; i < count; i++)
            logger.Info($"msg-{i}");

        await factory.FlushAsync();

        await Assert.That(sink.Messages.Count).IsEqualTo(count);

        // Verify all messages are present (order-independent via hashset)
        var expected = Enumerable.Range(0, count).Select(i => $"msg-{i}").ToHashSet();
        var received = sink.Messages.ToHashSet();
        await Assert.That(received.SetEquals(expected)).IsTrue();
    }
}
