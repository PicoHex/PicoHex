namespace PicoLog.Tests;

public sealed class CategoryFilterTests
{
    [Test]
    public async Task CategoryPrefix_FiltersOutEntries_BelowRuleThreshold()
    {
        var sink = new CollectingSink();
        var options = new LoggerFactoryOptions { MinLevel = LogLevel.Debug };
        options.FilterRules.Add(new LogFilterRule("Tests.Data", LogLevel.Error));

        await using var factory = new LoggerFactory([sink], options);
        var logger = factory.CreateLogger("Tests.Data.User");

        logger.Info("ignored-info");
        logger.Warning("ignored-warning");
        await logger.ErrorAsync("recorded-error");

        await factory.DisposeAsync();

        var entries = sink.Entries.ToArray();
        await Assert.That(entries).Count().IsEqualTo(1);
        await Assert.That(entries[0].Level).IsEqualTo(LogLevel.Error);
        await Assert.That(entries[0].Message).IsEqualTo("recorded-error");
    }

    [Test]
    public async Task GlobalMinLevel_IsFallback_WhenNoRuleMatches()
    {
        var sink = new CollectingSink();
        var options = new LoggerFactoryOptions { MinLevel = LogLevel.Warning };
        options.FilterRules.Add(new LogFilterRule("Tests.Data", LogLevel.Debug));

        await using var factory = new LoggerFactory([sink], options);
        var dataLogger = factory.CreateLogger("Tests.Data.Helper");
        var otherLogger = factory.CreateLogger("Tests.Other");

        await dataLogger.DebugAsync("data-debug-passes");

        otherLogger.Info("other-info-ignored");
        await otherLogger.WarningAsync("other-warning-passes");

        await factory.DisposeAsync();

        var entries = sink.Entries.ToArray();
        await Assert.That(entries).Count().IsEqualTo(2);
        await Assert
            .That(entries.Select(e => (e.Level, e.Message)).ToArray())
            .IsEquivalentTo(

                [
                    (LogLevel.Debug, (string?)"data-debug-passes"),
                    (LogLevel.Warning, (string?)"other-warning-passes")
                ]
            );
    }

    [Test]
    public async Task MultipleRules_MostRecentlyAddedWins()
    {
        var sink = new CollectingSink();
        var options = new LoggerFactoryOptions { MinLevel = LogLevel.Debug };
        options.FilterRules.Add(new LogFilterRule("Tests.Data", LogLevel.Warning));
        options.FilterRules.Add(new LogFilterRule("Tests.Data.User", LogLevel.Debug));

        await using var factory = new LoggerFactory([sink], options);
        var userLogger = factory.CreateLogger("Tests.Data.User.Profile");
        var genericLogger = factory.CreateLogger("Tests.Data.Generic");

        await userLogger.DebugAsync("user-debug-passes");

        genericLogger.Info("generic-info-ignored");
        await genericLogger.WarningAsync("generic-warning-passes");

        await factory.DisposeAsync();

        var entries = sink.Entries.ToArray();
        await Assert.That(entries).Count().IsEqualTo(2);
        await Assert
            .That(entries.Select(e => (e.Level, e.Message)).ToArray())
            .IsEquivalentTo(

                [
                    (LogLevel.Debug, (string?)"user-debug-passes"),
                    (LogLevel.Warning, (string?)"generic-warning-passes")
                ]
            );
    }

    [Test]
    public async Task FilterRules_WorkWithAsyncWrites()
    {
        var sink = new CollectingSink();
        var options = new LoggerFactoryOptions { MinLevel = LogLevel.Debug };
        options.FilterRules.Add(new LogFilterRule("Tests.Data", LogLevel.Error));

        await using var factory = new LoggerFactory([sink], options);
        var logger = factory.CreateLogger("Tests.Data");

        await logger.NoticeAsync("async-notice-ignored");
        await logger.WarningAsync("async-warning-ignored");
        await logger.ErrorAsync("async-error-recorded");

        await factory.DisposeAsync();

        var entries = sink.Entries.ToArray();
        await Assert.That(entries).Count().IsEqualTo(1);
        await Assert.That(entries[0].Level).IsEqualTo(LogLevel.Error);
        await Assert.That(entries[0].Message).IsEqualTo("async-error-recorded");
    }

    [Test]
    public async Task FilterRules_DontAffectOtherCategories()
    {
        var sink = new CollectingSink();
        var options = new LoggerFactoryOptions { MinLevel = LogLevel.Debug };
        options.FilterRules.Add(new LogFilterRule("Tests.Data", LogLevel.Critical));

        await using var factory = new LoggerFactory([sink], options);
        var dataLogger = factory.CreateLogger("Tests.Data");
        var otherLogger = factory.CreateLogger("Tests.Other");

        await dataLogger.ErrorAsync("data-error-ignored");
        await otherLogger.DebugAsync("other-debug-passes");

        await factory.DisposeAsync();

        var entries = sink.Entries.ToArray();
        await Assert.That(entries).Count().IsEqualTo(1);
        await Assert.That(entries[0].Level).IsEqualTo(LogLevel.Debug);
        await Assert.That(entries[0].Message).IsEqualTo("other-debug-passes");
    }

    [Test]
    public async Task LogFilterRule_WithMinLevelNone_DisablesCategoryPrefix()
    {
        var sink = new CollectingSink();
        var options = new LoggerFactoryOptions { MinLevel = LogLevel.Debug };
        options.FilterRules.Add(new LogFilterRule("Tests.Data", LogLevel.None));

        await using var factory = new LoggerFactory([sink], options);
        var dataLogger = factory.CreateLogger("Tests.Data");
        var otherLogger = factory.CreateLogger("Tests.Other");

        await dataLogger.EmergencyAsync("data-emergency-ignored");
        dataLogger.Error("data-error-ignored");

        await otherLogger.InfoAsync("other-info-passes");

        await factory.DisposeAsync();

        var entries = sink.Entries.ToArray();
        await Assert.That(entries).Count().IsEqualTo(1);
        await Assert.That(entries[0].Level).IsEqualTo(LogLevel.Info);
        await Assert.That(entries[0].Message).IsEqualTo("other-info-passes");
    }

    [Test]
    public async Task ExactCategoryName_MatchesPrefix()
    {
        var sink = new CollectingSink();
        var options = new LoggerFactoryOptions { MinLevel = LogLevel.Debug };
        options.FilterRules.Add(new LogFilterRule("Tests.Data", LogLevel.Error));

        await using var factory = new LoggerFactory([sink], options);
        var logger = factory.CreateLogger("Tests.Data");

        logger.Info("ignored-info");
        await logger.ErrorAsync("recorded-error");

        await factory.DisposeAsync();

        var entries = sink.Entries.ToArray();
        await Assert.That(entries).Count().IsEqualTo(1);
        await Assert.That(entries[0].Level).IsEqualTo(LogLevel.Error);
        await Assert.That(entries[0].Message).IsEqualTo("recorded-error");
    }

    [Test]
    public async Task NoMatchingPrefix_UsesGlobalMinLevel()
    {
        var sink = new CollectingSink();
        var options = new LoggerFactoryOptions { MinLevel = LogLevel.Error };

        await using var factory = new LoggerFactory([sink], options);
        var logger = factory.CreateLogger("Tests.Data");

        logger.Info("ignored-info");
        logger.Warning("ignored-warning");
        await logger.ErrorAsync("recorded-error");

        await factory.DisposeAsync();

        var entries = sink.Entries.ToArray();
        await Assert.That(entries).Count().IsEqualTo(1);
        await Assert.That(entries[0].Level).IsEqualTo(LogLevel.Error);
        await Assert.That(entries[0].Message).IsEqualTo("recorded-error");
    }

    [Test]
    public async Task PrefixNotSubstring_OnlyMatchesFromStart()
    {
        var sink = new CollectingSink();
        var options = new LoggerFactoryOptions { MinLevel = LogLevel.Debug };
        options.FilterRules.Add(new LogFilterRule("Data", LogLevel.Critical));

        await using var factory = new LoggerFactory([sink], options);
        var logger = factory.CreateLogger("Tests.Data");

        await logger.ErrorAsync("error-passes-global-debug");

        await factory.DisposeAsync();

        var entries = sink.Entries.ToArray();
        await Assert.That(entries).Count().IsEqualTo(1);
        await Assert.That(entries[0].Level).IsEqualTo(LogLevel.Error);
        await Assert.That(entries[0].Message).IsEqualTo("error-passes-global-debug");
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
