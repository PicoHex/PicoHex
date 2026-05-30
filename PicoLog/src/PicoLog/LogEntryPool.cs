namespace PicoLog;

internal static class LogEntryPool
{
    private const int MaxPoolSize = 256;
    private static readonly ConcurrentQueue<LogEntry> _pool = new();

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

        if (_pool.Count < MaxPoolSize)
            _pool.Enqueue(entry);
    }
}
