namespace PicoLog;

internal static class LogEntryPool
{
    private const int MaxPoolSize = 256;
    private static readonly ConcurrentQueue<LogEntry> _pool = new();

    /// <summary>
    /// Gets the current number of entries in the pool.
    /// Used for testing pool size enforcement.
    /// </summary>
    internal static int Count => _pool.Count;

    public static LogEntry Rent()
    {
        if (_pool.TryDequeue(out var entry))
        {
            entry.Reset();
            return entry;
        }

        return new LogEntry();
    }

    public static void Return(LogEntry entry)
    {
        if (entry is null)
            return;

        // Reset before returning to release references
        entry.Reset();

        // Bounded enqueue: when the pool is at capacity, excess entries are
        // silently dropped and will be garbage collected. A TOCTOU race exists
        // between the Count check and Enqueue, but the worst case is the pool
        // briefly exceeding MaxPoolSize by a small number — no data corruption.
        if (_pool.Count >= MaxPoolSize)
            return;

        _pool.Enqueue(entry);
    }
}
