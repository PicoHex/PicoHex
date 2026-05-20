namespace PicoLog;

public sealed class LoggerFactoryOptions
{
    public LogLevel MinLevel { get; set; } = LogLevel.Debug;

    public int QueueCapacity { get; set; } = 65535;

    public LogQueueFullMode QueueFullMode { get; set; } = LogQueueFullMode.DropOldest;

    public TimeSpan SyncWriteTimeout { get; set; } = TimeSpan.FromMilliseconds(250);

    /// <summary>
    /// When <c>true</c> (default), synchronous writes that timeout while a flush is in
    /// progress are silently dropped. When <c>false</c>, the <see cref="TimeoutException"/>
    /// propagates to the caller.
    /// </summary>
    public bool DropMessagesOnFlush { get; set; } = true;

    public Action<string, long>? OnMessagesDropped { get; set; }

    public TimeProvider TimestampProvider { get; set; } = TimeProvider.System;

    public IList<LogFilterRule> FilterRules { get; set; } = [];

    /// <summary>
    /// Maximum time to wait for sink writes to complete during shutdown drain.
    /// <see cref="TimeSpan.Zero"/> (default) means wait indefinitely.
    /// </summary>
    public TimeSpan ShutdownTimeout { get; set; } = TimeSpan.FromSeconds(30);

    public LoggerFactoryOptions CreateValidatedCopy()
    {
        if (QueueCapacity <= 0)
            throw new ArgumentOutOfRangeException(nameof(QueueCapacity));

        if (SyncWriteTimeout < TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(SyncWriteTimeout));

        if (ShutdownTimeout < TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(ShutdownTimeout));

        return new LoggerFactoryOptions
        {
            MinLevel = MinLevel,
            QueueCapacity = QueueCapacity,
            QueueFullMode = QueueFullMode,
            SyncWriteTimeout = SyncWriteTimeout,
            DropMessagesOnFlush = DropMessagesOnFlush,
            OnMessagesDropped = OnMessagesDropped,
            TimestampProvider = TimestampProvider,
            ShutdownTimeout = ShutdownTimeout,
            FilterRules = [.. FilterRules]
        };
    }
}

public sealed record LogFilterRule(string CategoryPrefix, LogLevel MinLevel);
