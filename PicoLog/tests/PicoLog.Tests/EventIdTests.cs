namespace PicoLog.Tests;

public sealed class EventIdTests
{
    [Test]
    public async Task EventId_FlowsThroughPipeline_ToLogEntry()
    {
        var sink = new CollectingSink();
        await using var factory = new LoggerFactory([sink]);
        var logger = factory.CreateLogger("Tests.EventId");

        logger.Log(LogLevel.Info, new EventId(42, "test-event"), "event-message");

        await factory.DisposeAsync();

        var entry = sink.Entries.Single();
        await Assert.That(entry.EventId.Id).IsEqualTo(42);
        await Assert.That(entry.EventId.Name).IsEqualTo("test-event");
    }

    [Test]
    public async Task EventId_AsyncPath_FlowsThroughPipeline()
    {
        var sink = new CollectingSink();
        await using var factory = new LoggerFactory([sink]);
        var logger = factory.CreateLogger("Tests.EventId");

        await logger.LogAsync(LogLevel.Warning, new EventId(202, "async-event"), "async-eventid");

        await factory.DisposeAsync();

        var entry = sink.Entries.Single();
        await Assert.That(entry.EventId.Id).IsEqualTo(202);
        await Assert.That(entry.EventId.Name).IsEqualTo("async-event");
    }

    [Test]
    public async Task ConsoleFormatter_RendersEventId_WhenNonZero()
    {
        var formatter = new ConsoleFormatter();
        var entry = new LogEntry
        {
            Timestamp = new DateTimeOffset(2026, 5, 9, 10, 0, 0, TimeSpan.Zero),
            Level = LogLevel.Info,
            Category = "Tests.Formatting",
            Message = "event-message",
            EventId = new EventId(42),
        };

        var rendered = formatter.Format(entry);
        await Assert.That(rendered).Contains("[E42]");
    }

    [Test]
    public async Task ConsoleFormatter_DoesNotRenderEventId_WhenZero()
    {
        var formatter = new ConsoleFormatter();
        var entry = new LogEntry
        {
            Timestamp = new DateTimeOffset(2026, 5, 9, 10, 0, 0, TimeSpan.Zero),
            Level = LogLevel.Info,
            Category = "Tests.Formatting",
            Message = "no-event-id",
            EventId = default,
        };

        var rendered = formatter.Format(entry);
        await Assert.That(rendered).DoesNotContain("[E0]");
        await Assert.That(rendered).DoesNotContain("[E");
    }

    [Test]
    public async Task EventId_WithFormattableString_PreservesBoth()
    {
        var sink = new CollectingSink();
        await using var factory = new LoggerFactory([sink]);
        var logger = factory.CreateLogger("Tests.EventId");

        logger.Log(
            LogLevel.Info,
            new EventId(99, "fmt-event"),
            (FormattableString)$"Value is {42}"
        );

        await factory.DisposeAsync();

        var entry = sink.Entries.Single();
        await Assert.That(entry.EventId.Id).IsEqualTo(99);
        await Assert.That(entry.EventId.Name).IsEqualTo("fmt-event");
        await Assert.That(entry.MessageTemplate).IsEqualTo("Value is {0}");
        await Assert.That(entry.MessageArgs).IsNotNull();
        await Assert.That(entry.MessageArgs!.Count).IsEqualTo(1);
        await Assert.That(entry.MessageArgs[0]).IsEqualTo(42);
    }

    [Test]
    public async Task EventId_WithFormattableString_AsyncPath()
    {
        var sink = new CollectingSink();
        await using var factory = new LoggerFactory([sink]);
        var logger = factory.CreateLogger("Tests.EventId");

        await logger.LogAsync(
            LogLevel.Error,
            new EventId(55, "async-fmt"),
            (FormattableString)$"Error code {404}"
        );

        await factory.DisposeAsync();

        var entry = sink.Entries.Single();
        await Assert.That(entry.EventId.Id).IsEqualTo(55);
        await Assert.That(entry.EventId.Name).IsEqualTo("async-fmt");
        await Assert.That(entry.MessageTemplate).IsEqualTo("Error code {0}");
        await Assert.That(entry.MessageArgs).IsNotNull();
        await Assert.That(entry.MessageArgs!.Count).IsEqualTo(1);
        await Assert.That(entry.MessageArgs[0]).IsEqualTo(404);
    }

    [Test]
    public async Task EventId_WithStructuredProperties_PreservesBoth()
    {
        var sink = new CollectingSink();
        await using var factory = new LoggerFactory([sink]);
        var logger = factory.CreateLogger("Tests.EventId");
        IReadOnlyList<KeyValuePair<string, object?>> properties = [new("tenant", "alpha")];

        logger.Log(
            LogLevel.Info,
            new EventId(77),
            "structured-with-eventid",
            properties,
            exception: null
        );

        await factory.DisposeAsync();

        var entry = sink.Entries.Single();
        await Assert.That(entry.EventId.Id).IsEqualTo(77);
        await Assert.That(entry.Properties).IsNotNull();
        await Assert.That(entry.Properties!.Count).IsEqualTo(1);
        await Assert.That(entry.Properties[0].Key).IsEqualTo("tenant");
        await Assert.That(entry.Properties[0].Value).IsEqualTo("alpha");
    }

    [Test]
    public async Task EventId_WithStructuredProperties_AsyncPath()
    {
        var sink = new CollectingSink();
        await using var factory = new LoggerFactory([sink]);
        var logger = factory.CreateLogger("Tests.EventId");
        IReadOnlyList<KeyValuePair<string, object?>> properties = [new("attempt", 3)];

        await logger.LogAsync(
            LogLevel.Warning,
            new EventId(88, "async-struct"),
            "async-structured-eventid",
            properties,
            exception: null
        );

        await factory.DisposeAsync();

        var entry = sink.Entries.Single();
        await Assert.That(entry.EventId.Id).IsEqualTo(88);
        await Assert.That(entry.EventId.Name).IsEqualTo("async-struct");
        await Assert.That(entry.Properties).IsNotNull();
        await Assert.That(entry.Properties!.Count).IsEqualTo(1);
        await Assert.That(entry.Properties[0].Key).IsEqualTo("attempt");
        await Assert.That(entry.Properties[0].Value).IsEqualTo(3);
    }

    [Test]
    public async Task EventId_WithFormattableStringAndProperties_PreservesAll()
    {
        var sink = new CollectingSink();
        await using var factory = new LoggerFactory([sink]);
        var logger = factory.CreateLogger("Tests.EventId");
        IReadOnlyList<KeyValuePair<string, object?>> properties =
        [
            new("tenant", "alpha"),
            new("attempt", 3),
        ];

        logger.Log(
            LogLevel.Info,
            new EventId(111, "full-event"),
            (FormattableString)$"Value is {42}",
            properties,
            exception: null
        );

        await factory.DisposeAsync();

        var entry = sink.Entries.Single();
        await Assert.That(entry.EventId.Id).IsEqualTo(111);
        await Assert.That(entry.EventId.Name).IsEqualTo("full-event");
        await Assert.That(entry.MessageTemplate).IsEqualTo("Value is {0}");
        await Assert.That(entry.MessageArgs).IsNotNull();
        await Assert.That(entry.MessageArgs!.Count).IsEqualTo(1);
        await Assert.That(entry.MessageArgs[0]).IsEqualTo(42);
        await Assert.That(entry.Properties).IsNotNull();
        await Assert.That(entry.Properties!.Count).IsEqualTo(2);
        await Assert.That(entry.Properties[0].Key).IsEqualTo("tenant");
        await Assert.That(entry.Properties[0].Value).IsEqualTo("alpha");
        await Assert.That(entry.Properties[1].Key).IsEqualTo("attempt");
        await Assert.That(entry.Properties[1].Value).IsEqualTo(3);
    }

    [Test]
    public async Task EventId_LoggerExtensions_Sync_ForwardsEventId()
    {
        var sink = new CollectingSink();
        await using var factory = new LoggerFactory([sink]);
        var logger = factory.CreateLogger("Tests.EventId");

        logger.Info(new EventId(1001, "ext-event"), "extension-message");

        await factory.DisposeAsync();

        var entry = sink.Entries.Single();
        await Assert.That(entry.EventId.Id).IsEqualTo(1001);
        await Assert.That(entry.EventId.Name).IsEqualTo("ext-event");
        await Assert.That(entry.Message).IsEqualTo("extension-message");
    }

    [Test]
    public async Task EventId_LoggerExtensions_Async_ForwardsEventId()
    {
        var sink = new CollectingSink();
        await using var factory = new LoggerFactory([sink]);
        var logger = factory.CreateLogger("Tests.EventId");

        await logger.WarningAsync(new EventId(2002, "ext-async"), "async-extension");

        await factory.DisposeAsync();

        var entry = sink.Entries.Single();
        await Assert.That(entry.EventId.Id).IsEqualTo(2002);
        await Assert.That(entry.EventId.Name).IsEqualTo("ext-async");
        await Assert.That(entry.Message).IsEqualTo("async-extension");
    }

    [Test]
    public async Task EventId_MultipleLevels_AllForwardEventId()
    {
        var sink = new CollectingSink();
        await using var factory = new LoggerFactory([sink]) { MinLevel = LogLevel.Trace };
        var logger = factory.CreateLogger("Tests.EventId");

        logger.Trace(new EventId(1), "trace");
        logger.Debug(new EventId(2), "debug");
        logger.Info(new EventId(3), "info");
        logger.Notice(new EventId(4), "notice");
        logger.Warning(new EventId(5), "warning");
        logger.Error(new EventId(6), "error");
        logger.Critical(new EventId(7), "critical");
        logger.Alert(new EventId(8), "alert");
        logger.Emergency(new EventId(9), "emergency");

        await factory.DisposeAsync();

        var entries = sink.Entries.OrderBy(e => e.EventId.Id).ToArray();
        await Assert.That(entries).Count().IsEqualTo(9);
        await Assert.That(entries[0].EventId.Id).IsEqualTo(1);
        await Assert.That(entries[1].EventId.Id).IsEqualTo(2);
        await Assert.That(entries[2].EventId.Id).IsEqualTo(3);
        await Assert.That(entries[3].EventId.Id).IsEqualTo(4);
        await Assert.That(entries[4].EventId.Id).IsEqualTo(5);
        await Assert.That(entries[5].EventId.Id).IsEqualTo(6);
        await Assert.That(entries[6].EventId.Id).IsEqualTo(7);
        await Assert.That(entries[7].EventId.Id).IsEqualTo(8);
        await Assert.That(entries[8].EventId.Id).IsEqualTo(9);
    }

    [Test]
    public async Task EventId_AsyncExtensionMethods_ForwardEventId()
    {
        var sink = new CollectingSink();
        await using var factory = new LoggerFactory([sink]);
        var logger = factory.CreateLogger("Tests.EventId");

        var x = 42;
        await logger.InfoAsync(new EventId(3003, "async-ext-fmt"), $"msg {x}");

        await factory.DisposeAsync();

        var entry = sink.Entries.Single();
        await Assert.That(entry.EventId.Id).IsEqualTo(3003);
        await Assert.That(entry.EventId.Name).IsEqualTo("async-ext-fmt");
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
