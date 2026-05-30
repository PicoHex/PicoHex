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
}
