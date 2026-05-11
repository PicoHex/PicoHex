namespace PicoLog.Tests;

public sealed class DeferredFormattingTests
{
    [Test]
    public async Task FormattableString_CapturesTemplateAndArgs_InLogEntry()
    {
        var sink = new TestSink();
        await using var factory = new LoggerFactory([sink]);
        var logger = factory.CreateLogger("Tests.DeferredFormatting");

        logger.Log(LogLevel.Info, (FormattableString)$"Value is {42}");

        await factory.DisposeAsync();

        var entry = sink.Entries.Single();
        await Assert.That(entry.MessageTemplate).IsEqualTo("Value is {0}");
        await Assert.That(entry.Message).IsNull();
        await Assert.That(entry.MessageArgs).IsNotNull();
        await Assert.That(entry.MessageArgs!.Count).IsEqualTo(1);
        await Assert.That(entry.MessageArgs[0]).IsEqualTo(42);
    }

    [Test]
    public async Task FormattableString_DoesNotEagerlyFormat_UsingExtensionMethod()
    {
        var sink = new TestSink();
        await using var factory = new LoggerFactory([sink]);
        var logger = factory.CreateLogger("Tests.DeferredFormatting");

        logger.Info((FormattableString)$"Value is {42}");

        await factory.DisposeAsync();

        var entry = sink.Entries.Single();
        await Assert.That(entry.MessageTemplate).IsEqualTo("Value is {0}");
        await Assert.That(entry.Message).IsNull();
        await Assert.That(entry.MessageArgs).IsNotNull();
        await Assert.That(entry.MessageArgs!.Count).IsEqualTo(1);
        await Assert.That(entry.MessageArgs[0]).IsEqualTo(42);
    }

    [Test]
    public async Task FormattableString_RespectsMinLevel_Filtering()
    {
        var sink = new TestSink();
        await using var factory = new LoggerFactory([sink]) { MinLevel = LogLevel.Warning };
        var logger = factory.CreateLogger("Tests.DeferredFormatting");

        logger.Log(LogLevel.Info, (FormattableString)$"Should be filtered {42}");
        logger.Log(LogLevel.Warning, (FormattableString)$"Should be recorded {99}");

        await factory.DisposeAsync();

        var entries = sink.Entries.ToArray();
        await Assert.That(entries).Count().IsEqualTo(1);
        await Assert.That(entries[0].MessageTemplate).IsEqualTo("Should be recorded {0}");
        await Assert.That(entries[0].MessageArgs).IsNotNull();
        var args = entries[0].MessageArgs;
        await Assert.That(args).IsNotNull();
        await Assert.That(args!.Count).IsEqualTo(1);
        await Assert.That(args[0]).IsEqualTo(99);
    }

    [Test]
    public async Task FormattableString_WithStructuredProperties_PreservesBoth()
    {
        var sink = new TestSink();
        await using var factory = new LoggerFactory([sink]);
        var logger = factory.CreateLogger("Tests.DeferredFormatting");
        IReadOnlyList<KeyValuePair<string, object?>> properties =
        [
            new("tenant", "alpha"),
            new("attempt", 3)
        ];

        logger.Log(LogLevel.Info, (FormattableString)$"Value is {42}", properties, exception: null);

        await factory.DisposeAsync();

        var entry = sink.Entries.Single();
        await Assert.That(entry.MessageTemplate).IsEqualTo("Value is {0}");
        await Assert.That(entry.Message).IsNull();
        await Assert.That(entry.MessageArgs).IsNotNull();
        await Assert.That(entry.MessageArgs!.Count).IsEqualTo(1);
        await Assert.That(entry.MessageArgs[0]).IsEqualTo(42);
        await Assert.That(entry.Properties).IsNotNull();
        await Assert.That(entry.Properties!).Count().IsEqualTo(2);
        await Assert.That(entry.Properties[0].Key).IsEqualTo("tenant");
        await Assert.That(entry.Properties[0].Value).IsEqualTo("alpha");
        await Assert.That(entry.Properties[1].Key).IsEqualTo("attempt");
        await Assert.That(entry.Properties[1].Value).IsEqualTo(3);
    }

    [Test]
    public async Task FormattableString_AsyncPath_PreservesTemplateAndArgs()
    {
        var sink = new TestSink();
        await using var factory = new LoggerFactory([sink]);
        var logger = factory.CreateLogger("Tests.DeferredFormatting");

        await logger.LogAsync(LogLevel.Info, (FormattableString)$"Async value {99}");

        await factory.DisposeAsync();

        var entry = sink.Entries.Single();
        await Assert.That(entry.MessageTemplate).IsEqualTo("Async value {0}");
        await Assert.That(entry.Message).IsNull();
        await Assert.That(entry.MessageArgs).IsNotNull();
        await Assert.That(entry.MessageArgs!.Count).IsEqualTo(1);
        await Assert.That(entry.MessageArgs[0]).IsEqualTo(99);
    }

    [Test]
    public async Task ConsoleFormatter_RendersMessageTemplateAndArgs_WhenPresent()
    {
        var formatter = new ConsoleFormatter();
        var entry = new LogEntry
        {
            Timestamp = new DateTimeOffset(2026, 5, 9, 10, 0, 0, TimeSpan.Zero),
            Level = LogLevel.Info,
            Category = "Tests.Formatting",
            Message = null,
            MessageTemplate = "Value is {0} and {1}",
            MessageArgs = new object[] { 42, "hello" }
        };

        var rendered = formatter.Format(entry);
        await Assert.That(rendered).Contains("Value is 42 and hello");
    }

    [Test]
    public async Task ConsoleFormatter_FallsBackToMessage_WhenNoTemplateOrArgs()
    {
        var formatter = new ConsoleFormatter();
        var entry = new LogEntry
        {
            Timestamp = new DateTimeOffset(2026, 5, 9, 10, 0, 0, TimeSpan.Zero),
            Level = LogLevel.Info,
            Category = "Tests.Formatting",
            Message = "plain message",
            MessageTemplate = null,
            MessageArgs = null
        };

        var rendered = formatter.Format(entry);
        await Assert.That(rendered).Contains("plain message");
    }

    [Test]
    public async Task FormattableString_WithMultipleArgs_PreservesAll()
    {
        var sink = new TestSink();
        await using var factory = new LoggerFactory([sink]);
        var logger = factory.CreateLogger("Tests.DeferredFormatting");

        logger.Log(LogLevel.Info, (FormattableString)$"A={1} B={2} C={3}");

        await factory.DisposeAsync();

        var entry = sink.Entries.Single();
        await Assert.That(entry.MessageTemplate).IsEqualTo("A={0} B={1} C={2}");
        await Assert.That(entry.Message).IsNull();
        await Assert.That(entry.MessageArgs).IsNotNull();
        await Assert.That(entry.MessageArgs!.Count).IsEqualTo(3);
        await Assert.That(entry.MessageArgs[0]).IsEqualTo(1);
        await Assert.That(entry.MessageArgs[1]).IsEqualTo(2);
        await Assert.That(entry.MessageArgs[2]).IsEqualTo(3);
    }

    [Test]
    public async Task FormattableString_WithNullArg_PreservesNull()
    {
        var sink = new TestSink();
        await using var factory = new LoggerFactory([sink]);
        var logger = factory.CreateLogger("Tests.DeferredFormatting");

        logger.Log(LogLevel.Info, (FormattableString)$"Value is {null}");

        await factory.DisposeAsync();

        var entry = sink.Entries.Single();
        await Assert.That(entry.MessageTemplate).IsEqualTo("Value is {0}");
        await Assert.That(entry.Message).IsNull();
        await Assert.That(entry.MessageArgs).IsNotNull();
        await Assert.That(entry.MessageArgs!.Count).IsEqualTo(1);
        await Assert.That(entry.MessageArgs[0]).IsNull();
    }

    [Test]
    public async Task FormattableString_ConsoleFormatter_FullOutput()
    {
        var formatter = new ConsoleFormatter();
        var entry = new LogEntry
        {
            Timestamp = new DateTimeOffset(2026, 5, 9, 10, 0, 0, TimeSpan.Zero),
            Level = LogLevel.Info,
            Category = "Tests.Formatting",
            Message = null,
            MessageTemplate = "Value is {0}",
            MessageArgs = new object[] { 42 }
        };

        var rendered = formatter.Format(entry);
        await Assert.That(rendered).Contains("Value is 42");
    }

    [Test]
    public async Task FormattableString_SyncVsAsync_BothPreserveTemplate()
    {
        var sink = new TestSink();
        await using var factory = new LoggerFactory([sink]);
        var logger = factory.CreateLogger("Tests.DeferredFormatting");

        logger.Log(LogLevel.Info, (FormattableString)$"User {42}");
        await logger.LogAsync(LogLevel.Info, (FormattableString)$"User {42}");

        await factory.DisposeAsync();

        var entries = sink.Entries.ToArray();
        await Assert.That(entries).Count().IsEqualTo(2);
        await Assert.That(entries[0].MessageTemplate).IsEqualTo("User {0}");
        await Assert.That(entries[1].MessageTemplate).IsEqualTo("User {0}");
        await Assert.That(entries[0].Message).IsNull();
        await Assert.That(entries[1].Message).IsNull();
        await Assert.That(entries[0].MessageArgs).IsNotNull();
        await Assert.That(entries[0].MessageArgs!.Count).IsEqualTo(1);
        await Assert.That(entries[0].MessageArgs![0]).IsEqualTo(42);
        await Assert.That(entries[1].MessageArgs).IsNotNull();
        await Assert.That(entries[1].MessageArgs!.Count).IsEqualTo(1);
        await Assert.That(entries[1].MessageArgs![0]).IsEqualTo(42);
    }

    private sealed class TestSink : ILogSink
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
