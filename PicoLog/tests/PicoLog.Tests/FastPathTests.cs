namespace PicoLog.Tests;

public sealed class FastPathTests
{
    private sealed class FastSink : IFastLogSink
    {
        private readonly ConcurrentQueue<string> _messages = new();

        public IReadOnlyCollection<string> Messages => _messages.ToArray();

        public Task WriteAsync(LogEntry entry, CancellationToken cancellationToken = default)
        {
            // Copy — entry may be returned to pool after dispatch
            _messages.Enqueue(entry.Message ?? string.Empty);
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
        await Assert.That(sink.Messages.Count).IsEqualTo(1);
        await Assert.That(sink.Messages.Single()).IsEqualTo("hello");
    }
}
