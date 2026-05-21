namespace PicoLog;

internal enum LogWriteResult
{
    Accepted,
    AcceptedAfterEviction,
    DroppedNewWrite,
    RejectedAfterShutdown
}

internal sealed class InternalLoggerQueue
{
    private readonly ChannelWriter<LogEntry> _writer;
    private readonly ChannelReader<LogEntry> _reader;
    private readonly LoggerFactoryRuntime _runtime;
    private readonly int _queueCapacity;
    private readonly LogQueueFullMode _queueFullMode;
    private readonly TimeSpan _syncWriteTimeout;
    private int _shutdownStarted;

    public InternalLoggerQueue(LoggerFactoryRuntime runtime)
    {
        _runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
        _queueCapacity = _runtime.QueueCapacity;
        _queueFullMode = _runtime.QueueFullMode;
        _syncWriteTimeout = _runtime.SyncWriteTimeout;
        var channel = Channel.CreateBounded<LogEntry>(
            new BoundedChannelOptions(_queueCapacity)
            {
                FullMode = _queueFullMode switch
                {
                    LogQueueFullMode.DropOldest => BoundedChannelFullMode.DropOldest,
                    LogQueueFullMode.DropWrite => BoundedChannelFullMode.DropWrite,
                    LogQueueFullMode.Wait => BoundedChannelFullMode.Wait,
                    _ => BoundedChannelFullMode.DropOldest
                },
                SingleReader = true
            }
        );
        _writer = channel.Writer;
        _reader = channel.Reader;
    }

    public ValueTask<bool> WaitToReadAsync() => _reader.WaitToReadAsync();

    public bool TryRead([System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out LogEntry? entry) =>
        _reader.TryRead(out entry);

    public LogWriteResult TryEnqueueSync(LogEntry entry) =>
        _queueFullMode switch
        {
            LogQueueFullMode.Wait => TryEnqueueSyncWithWait(entry),
            LogQueueFullMode.DropWrite => TryEnqueueSyncDropWrite(entry),
            _ => TryEnqueueSyncDropOldest(entry)
        };

    public ValueTask<LogWriteResult> TryEnqueueAsync(
        LogEntry entry,
        CancellationToken cancellationToken
    ) =>
        _queueFullMode switch
        {
            LogQueueFullMode.Wait => TryEnqueueAsyncWithWaitAsync(entry, cancellationToken),
            LogQueueFullMode.DropWrite => ValueTask.FromResult(TryEnqueueSyncDropWrite(entry)),
            _ => ValueTask.FromResult(TryEnqueueSyncDropOldest(entry))
        };

    public void Complete()
    {
        Volatile.Write(ref _shutdownStarted, 1);
        _writer.TryComplete();
    }

    public long GetQueuedEntryCount() => _reader.Count;

    private LogWriteResult TryEnqueueSyncDropOldest(LogEntry entry)
    {
        // Detect eviction by checking channel fullness before writing.
        // BoundedChannel with DropOldest silently evicts the oldest
        // entry when full; the channel's own Count is authoritative.
        var wasFull = _reader.Count >= _queueCapacity;

        if (!_writer.TryWrite(entry))
            return DetermineFailedWriteResult();

        _runtime.RecordEntryAccepted();

        return wasFull ? LogWriteResult.AcceptedAfterEviction : LogWriteResult.Accepted;
    }

    private LogWriteResult TryEnqueueSyncDropWrite(LogEntry entry)
    {
        if (_reader.Count >= _queueCapacity)
            return LogWriteResult.DroppedNewWrite;

        if (!_writer.TryWrite(entry))
            return DetermineFailedWriteResult();

        _runtime.RecordEntryAccepted();
        return LogWriteResult.Accepted;
    }

    private LogWriteResult TryEnqueueSyncWithWait(LogEntry entry)
    {
        if (_writer.TryWrite(entry))
        {
            _runtime.RecordEntryAccepted();
            return LogWriteResult.Accepted;
        }

        // Fast-exit if shutdown already started: a completed channel will
        // never accept TryWrite again, and ChannelWriter.TryWrite does not
        // throw on completion (it just returns false), so without this
        // check we would burn the full SyncWriteTimeout sleeping in the
        // backoff loop. Under TUnit parallel + coverage instrumentation
        // (linux-x64) that turns shutdown into a serialized stall that
        // pins ThreadPool workers and trips the test-runner's hang
        // detection.
        if (Volatile.Read(ref _shutdownStarted) != 0)
            return LogWriteResult.RejectedAfterShutdown;

        var startTimestamp = Stopwatch.GetTimestamp();
        var backoffMs = 1;
        const int maxBackoffMs = 25;
        const int backoffFactor = 2;

        try
        {
            while (true)
            {
                if (Volatile.Read(ref _shutdownStarted) != 0)
                    return LogWriteResult.RejectedAfterShutdown;

                var remaining = _syncWriteTimeout - Stopwatch.GetElapsedTime(startTimestamp);
                if (remaining <= TimeSpan.Zero)
                    return LogWriteResult.DroppedNewWrite;

                var sleepMs = (int)Math.Min(backoffMs, Math.Ceiling(remaining.TotalMilliseconds));
                Thread.Sleep(Math.Max(1, sleepMs));

                if (_writer.TryWrite(entry))
                {
                    _runtime.RecordEntryAccepted();
                    return LogWriteResult.Accepted;
                }

                backoffMs = Math.Min(backoffMs * backoffFactor, maxBackoffMs);
            }
        }
        catch (ChannelClosedException)
        {
            return DetermineFailedWriteResult();
        }
    }

    private async ValueTask<LogWriteResult> TryEnqueueAsyncWithWaitAsync(
        LogEntry entry,
        CancellationToken cancellationToken
    )
    {
        try
        {
            while (true)
            {
                if (_writer.TryWrite(entry))
                {
                    _runtime.RecordEntryAccepted();
                    return LogWriteResult.Accepted;
                }

                if (!await _writer.WaitToWriteAsync(cancellationToken).ConfigureAwait(false))
                    return DetermineFailedWriteResult();

                if (!_writer.TryWrite(entry))
                    continue;

                _runtime.RecordEntryAccepted();
                return LogWriteResult.Accepted;
            }
        }
        catch (ChannelClosedException)
        {
            return DetermineFailedWriteResult();
        }
    }

    private LogWriteResult DetermineFailedWriteResult() =>
        Volatile.Read(ref _shutdownStarted) != 0 || !_runtime.IsAcceptingWrites
            ? LogWriteResult.RejectedAfterShutdown
            : LogWriteResult.DroppedNewWrite;
}
