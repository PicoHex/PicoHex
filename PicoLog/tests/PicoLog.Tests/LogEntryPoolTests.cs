namespace PicoLog.Tests;

public sealed class LogEntryPoolTests
{
    [Test]
    public async Task Rent_ReturnsInstance_WithDefaultValues()
    {
        var entry = LogEntryPool.Rent();

        await Assert.That(entry).IsNotNull();
        await Assert.That(entry.Timestamp).IsEqualTo(default(DateTimeOffset));
        await Assert.That(entry.Message).IsNull();
        await Assert.That(entry.Category).IsNull();
    }

    [Test]
    public async Task ReturnAndRent_ReusesInstance()
    {
        var first = LogEntryPool.Rent();
        first.Message = "hello";
        LogEntryPool.Return(first);

        var second = LogEntryPool.Rent();

        await Assert.That(ReferenceEquals(first, second)).IsTrue();
        // After Rent, the entry should be reset
        await Assert.That(second.Message).IsNull();
    }

    [Test]
    public async Task RentMultipleTimes_WhenPoolEmpty_ReturnsNewInstances()
    {
        var a = LogEntryPool.Rent();
        var b = LogEntryPool.Rent();

        await Assert.That(ReferenceEquals(a, b)).IsFalse();
    }

    [Test]
    public async Task Return_WhenPoolExceedsMaxSize_DropsExcessEntries()
    {
        // 🔴 RED: MaxPoolSize = 256 is declared but never enforced.
        // Return() enqueues unconditionally, allowing unbounded growth.
        // After fix, the pool should drop entries beyond MaxPoolSize.

        var overflow = 300; // > MaxPoolSize (256)
        var entries = new LogEntry[overflow];

        // Rent and return many entries to trigger pool overflow
        for (var i = 0; i < overflow; i++)
            entries[i] = LogEntryPool.Rent();

        for (var i = 0; i < overflow; i++)
            LogEntryPool.Return(entries[i]);

        // Assert: pool should not exceed MaxPoolSize after dropping excess
        await Assert.That(LogEntryPool.Count).IsLessThanOrEqualTo(256);

        // Assert: pool should still return valid entries (not corrupted)
        var pooled = LogEntryPool.Rent();
        await Assert.That(pooled).IsNotNull();

        // Return it for cleanup
        LogEntryPool.Return(pooled);
    }
}
