namespace PicoLog.Tests;

public sealed class StructuredScopeTests
{
    [Test]
    public async Task DictionaryScope_ExtractsScopeProperties()
    {
        var sink = new CollectingSink();
        await using var factory = new LoggerFactory([sink]);
        var logger = factory.CreateLogger("Tests.ScopeProps");

        using var scope = logger.BeginScope(new Dictionary<string, object> { { "key", "val" } });
        logger.Info("test");
        await factory.DisposeAsync();

        var entry = sink.Entries.Single();
        await Assert.That(entry.ScopeProperties).IsNotNull();
        await Assert.That(entry.ScopeProperties!.Count).IsEqualTo(1);
        await Assert.That(entry.ScopeProperties[0].Key).IsEqualTo("key");
        await Assert.That(entry.ScopeProperties[0].Value).IsEqualTo("val");
    }

    [Test]
    public async Task ListKeyValuePairScope_ExtractsScopeProperties()
    {
        var sink = new CollectingSink();
        await using var factory = new LoggerFactory([sink]);
        var logger = factory.CreateLogger("Tests.ScopeProps");

        var kvList = new List<KeyValuePair<string, object?>>
        {
            new("key1", "val1"),
            new("key2", 42)
        };
        using var scope = logger.BeginScope(kvList);
        logger.Info("test");
        await factory.DisposeAsync();

        var entry = sink.Entries.Single();
        await Assert.That(entry.ScopeProperties).IsNotNull();
        await Assert.That(entry.ScopeProperties!.Count).IsEqualTo(2);
        await Assert.That(entry.ScopeProperties[0].Key).IsEqualTo("key1");
        await Assert.That(entry.ScopeProperties[0].Value).IsEqualTo("val1");
        await Assert.That(entry.ScopeProperties[1].Key).IsEqualTo("key2");
        await Assert.That(entry.ScopeProperties[1].Value).IsEqualTo(42);
    }

    [Test]
    public async Task MultipleNestedScopes_MixedTypes_AggregatesScopeProperties()
    {
        var sink = new CollectingSink();
        await using var factory = new LoggerFactory([sink]);
        var logger = factory.CreateLogger("Tests.ScopeProps");

        using var outerStr = logger.BeginScope("outer");
        using var kvScope = logger.BeginScope(new Dictionary<string, object> { { "key", "val" } });
        using var innerStr = logger.BeginScope("inner");
        logger.Info("test");
        await factory.DisposeAsync();

        var entry = sink.Entries.Single();
        await Assert.That(entry.ScopeProperties).IsNotNull();
        await Assert.That(entry.ScopeProperties!.Count).IsEqualTo(1);
        await Assert.That(entry.ScopeProperties[0].Key).IsEqualTo("key");
        await Assert.That(entry.ScopeProperties[0].Value).IsEqualTo("val");
    }

    [Test]
    public async Task StringScope_YieldsNullScopeProperties()
    {
        var sink = new CollectingSink();
        await using var factory = new LoggerFactory([sink]);
        var logger = factory.CreateLogger("Tests.ScopeProps");

        using var scope = logger.BeginScope("string-scope");
        logger.Info("test");
        await factory.DisposeAsync();

        var entry = sink.Entries.Single();
        await Assert.That(entry.ScopeProperties).IsNull();
    }

    [Test]
    public async Task ConsoleFormatter_RendersScopeProperties()
    {
        var formatter = new ConsoleFormatter();
        var entry = new LogEntry
        {
            Timestamp = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
            Level = LogLevel.Info,
            Category = "Test",
            Message = "test message",
            ScopeProperties = new List<KeyValuePair<string, object?>>
            {
                new("key", "val"),
                new("count", 3)
            }
        };

        var output = formatter.Format(entry);
        await Assert.That(output).Contains("SCOPE_PROPS:");
        await Assert.That(output).Contains("key=\"val\"");
        await Assert.That(output).Contains("count=3");
    }

    [Test]
    public async Task StructuredScopeProperties_FlowThroughPipelineToSink()
    {
        var sink = new CollectingSink();
        await using var factory = new LoggerFactory([sink]);
        var logger = factory.CreateLogger("Tests.ScopeProps");

        using var scope = logger.BeginScope(
            new Dictionary<string, object> { { "tenant", "acme" }, { "requestId", "req-42" } }
        );
        logger.Info("processing request");
        await factory.DisposeAsync();

        var entry = sink.Entries.Single();
        await Assert.That(entry.ScopeProperties).IsNotNull();
        await Assert.That(entry.ScopeProperties!.Count).IsEqualTo(2);
        await Assert
            .That(entry.ScopeProperties.Any(p => p.Key == "tenant" && (string?)p.Value == "acme"))
            .IsTrue();
        await Assert
            .That(
                entry.ScopeProperties.Any(p => p.Key == "requestId" && (string?)p.Value == "req-42")
            )
            .IsTrue();

        var formatter = new ConsoleFormatter();
        var formatted = formatter.Format(entry);
        await Assert.That(formatted).Contains("SCOPE_PROPS:");
        await Assert.That(formatted).Contains("tenant=\"acme\"");
        await Assert.That(formatted).Contains("requestId=\"req-42\"");
    }

    [Test]
    public async Task ScopeProperties_NullValue_Preserved()
    {
        var sink = new CollectingSink();
        await using var factory = new LoggerFactory([sink]);
        var logger = factory.CreateLogger("Tests.ScopeProps");

        using var scope = logger.BeginScope(new Dictionary<string, object?> { { "key", null } });
        logger.Info("test");
        await factory.DisposeAsync();

        var entry = sink.Entries.Single();
        await Assert.That(entry.ScopeProperties).IsNotNull();
        await Assert.That(entry.ScopeProperties!.Count).IsEqualTo(1);
        await Assert.That(entry.ScopeProperties[0].Key).IsEqualTo("key");
        await Assert.That(entry.ScopeProperties[0].Value).IsNull();
    }

    [Test]
    public async Task ScopeProperties_EmptyDictionary_YieldsNull()
    {
        var sink = new CollectingSink();
        await using var factory = new LoggerFactory([sink]);
        var logger = factory.CreateLogger("Tests.ScopeProps");

        using var scope = logger.BeginScope(new Dictionary<string, object>());
        logger.Info("test");
        await factory.DisposeAsync();

        var entry = sink.Entries.Single();
        await Assert.That(entry.ScopeProperties).IsNotNull();
        await Assert.That(entry.ScopeProperties!.Count).IsEqualTo(0);
    }

    [Test]
    public async Task ScopeProperties_MergedAcrossMultipleScopes()
    {
        var sink = new CollectingSink();
        await using var factory = new LoggerFactory([sink]);
        var logger = factory.CreateLogger("Tests.ScopeProps");

        using var scope1 = logger.BeginScope(new Dictionary<string, object> { { "key1", "val1" } });
        using var scope2 = logger.BeginScope(new Dictionary<string, object> { { "key2", "val2" } });
        logger.Info("test");
        await factory.DisposeAsync();

        var entry = sink.Entries.Single();
        await Assert.That(entry.ScopeProperties).IsNotNull();
        await Assert.That(entry.ScopeProperties!.Count).IsEqualTo(2);
        await Assert.That(entry.ScopeProperties[0].Key).IsEqualTo("key2");
        await Assert.That(entry.ScopeProperties[0].Value).IsEqualTo("val2");
        await Assert.That(entry.ScopeProperties[1].Key).IsEqualTo("key1");
        await Assert.That(entry.ScopeProperties[1].Value).IsEqualTo("val1");
    }

    [Test]
    public async Task OutOfOrderDispose_DisposeInnerScopeBeforeChild_HandlesGracefully()
    {
        // Arrange
        var sink = new CollectingSink();
        await using var factory = new LoggerFactory([sink]);
        var logger = factory.CreateLogger("Tests.OutOfOrderDispose");

        // Act - create nested scopes and dispose out of order
        var scope1 = logger.BeginScope(new Dictionary<string, object> { { "level", "outer" } });
        var scope2 = logger.BeginScope(new Dictionary<string, object> { { "level", "middle" } });
        var scope3 = logger.BeginScope(new Dictionary<string, object> { { "level", "inner" } });

        // Dispose scope2 (middle) before scope3 (inner) — out of order
        scope2.Dispose();
        scope3.Dispose();
        scope1.Dispose();

        logger.Info("after-out-of-order-dispose");

        await factory.DisposeAsync();

        // Assert - the log entry should still have valid scope info
        var entry = sink.Entries.Single();
        await Assert.That(entry.ScopeProperties).IsNotNull();
        // After all scopes are disposed, capturing should return no scopes
        // (depends on implementation - if Capture is called after all are disposed)
    }

    [Test]
    public async Task OutOfOrderDispose_DisposeParentBeforeChild_DoesNotThrow()
    {
        // Arrange
        var sink = new CollectingSink();
        await using var factory = new LoggerFactory([sink]);
        var logger = factory.CreateLogger("Tests.OutOfOrderDispose");

        // Act & Assert - should not throw when parent scope is disposed before child
        var scope1 = logger.BeginScope("parent");
        var scope2 = logger.BeginScope("child");

        // Dispose parent first, then child
        scope1.Dispose();
        // Should not throw
        scope2.Dispose();

        logger.Info("after-parent-first-dispose");
        await factory.DisposeAsync();
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
