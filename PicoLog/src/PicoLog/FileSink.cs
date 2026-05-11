namespace PicoLog;

public sealed class FileSink : ILogSink, IFlushableLogSink
{
    private readonly Channel<string> _channel;
    private readonly ILogFormatter _formatter;
    private readonly FileSinkOptions _options;
    private readonly StreamWriter _writer;
    private readonly Task _processingTask;
    private readonly Lock _batchDelayStateLock = new();
    private readonly SemaphoreSlim _flushSemaphore = new(1, 1);
    private readonly FlushQuiesceCoordinator _flushQuiesceCoordinator = new();
    private int _disposeState;
    private int _activeDequeuedMessages;
    private int _activeBatchOperations;
    private CancellationTokenSource? _batchDelayCancellationSource;

    public FileSink(ILogFormatter formatter, string filePath = FileSinkOptions.DefaultFilePath)
        : this(formatter, new FileSinkOptions { FilePath = filePath }) { }

    public FileSink(ILogFormatter formatter, FileSinkOptions options)
    {
        _formatter = formatter ?? throw new ArgumentNullException(nameof(formatter));
        _options = (
            options ?? throw new ArgumentNullException(nameof(options))
        ).CreateValidatedCopy();

        var fullPath = Path.GetFullPath(_options.FilePath);
        var directory =
            Path.GetDirectoryName(fullPath)
            ?? throw new ArgumentException(
                $"File path must not be a root directory: '{_options.FilePath}'",
                nameof(options)
            );

        if (!Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        _channel = Channel.CreateBounded<string>(
            new BoundedChannelOptions(_options.QueueCapacity)
            {
                FullMode = BoundedChannelFullMode.Wait,
                SingleReader = true,
                AllowSynchronousContinuations = false
            }
        );

        var fileStream = new FileStream(
            fullPath,
            FileMode.OpenOrCreate,
            FileAccess.Write,
            FileShare.Read,
            bufferSize: 4096,
            useAsync: true
        );

        try
        {
            fileStream.Seek(0, SeekOrigin.End);
            _writer = new StreamWriter(fileStream, Encoding.UTF8);
        }
        catch
        {
            fileStream.Dispose();
            throw;
        }
        _processingTask = ProcessWritesAsync().AsTask();
    }

    public async Task WriteAsync(LogEntry entry, CancellationToken cancellationToken = default)
    {
        if (Volatile.Read(ref _disposeState) != 0)
            return;

        var message = _formatter.Format(entry);

        try
        {
            await _channel.Writer.WriteAsync(message, cancellationToken).ConfigureAwait(false);
        }
        catch (ChannelClosedException)
        {
            // Ignore writes that race with shutdown.
        }
    }

    public async ValueTask FlushAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposeState) != 0, this);

        await _flushSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposeState) != 0, this);

            await BlockWritesAsync(cancellationToken).ConfigureAwait(false);
            CancelBatchDelayWait();
            await WaitForIdleAsync(cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            await _writer.FlushAsync().ConfigureAwait(false);
        }
        finally
        {
            ResumeWrites();
            _flushSemaphore.Release();
        }
    }

    private async ValueTask ProcessWritesAsync()
    {
        var batch = new List<string>(_options.BatchSize);

        while (await _channel.Reader.WaitToReadAsync().ConfigureAwait(false))
        {
            while (true)
            {
                BeginDequeuedMessage();

                if (!_channel.Reader.TryRead(out var message))
                {
                    EndDequeuedMessage();
                    break;
                }

                batch.Add(message);

                try
                {
                    BeginBatch();

                    try
                    {
                        await DrainBatchAsync(batch).ConfigureAwait(false);
                    }
                    finally
                    {
                        EndBatch();
                    }
                }
                finally
                {
                    EndDequeuedMessage();
                }
            }
        }
    }

    private async ValueTask DrainBatchAsync(List<string> batch)
    {
        if (_options.FlushInterval > TimeSpan.Zero)
        {
            while (batch.Count < _options.BatchSize)
            {
                if (IsFlushPending())
                    break;

                using var cts = new CancellationTokenSource(_options.FlushInterval);
                RegisterBatchDelayCancellationSource(cts);

                try
                {
                    var message = await _channel.Reader.ReadAsync(cts.Token).ConfigureAwait(false);
                    batch.Add(message);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (ChannelClosedException)
                {
                    break;
                }
                finally
                {
                    ClearBatchDelayCancellationSource(cts);
                }
            }
        }
        else
        {
            while (batch.Count < _options.BatchSize && _channel.Reader.TryRead(out var message))
            {
                batch.Add(message);
            }
        }

        foreach (var message in batch)
            await _writer.WriteLineAsync(message).ConfigureAwait(false);

        await _writer.FlushAsync().ConfigureAwait(false);
        batch.Clear();
    }

    /// <summary>
    /// Synchronously disposes the sink by blocking on <see cref="DisposeAsync"/>.
    /// Prefer calling <see cref="DisposeAsync"/> directly.
    /// </summary>
    public void Dispose()
    {
        DisposeAsync().AsTask().GetAwaiter().GetResult();
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposeState, 1) != 0)
            return;

        CancelBatchDelayWait();
        _channel.Writer.TryComplete();

        Exception? processingException = null;

        try
        {
            await _processingTask.ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            processingException = ex;
        }

        try
        {
            await _writer.FlushAsync().ConfigureAwait(false);
        }
        finally
        {
            await _writer.DisposeAsync().ConfigureAwait(false);
        }

        await _flushSemaphore.WaitAsync().ConfigureAwait(false);
        _flushSemaphore.Dispose();

        if (processingException is not null)
            ExceptionDispatchInfo.Throw(processingException);
    }

    private ValueTask BlockWritesAsync(CancellationToken cancellationToken) =>
        _flushQuiesceCoordinator.BlockWritesAsync(cancellationToken);

    private ValueTask WaitForIdleAsync(CancellationToken cancellationToken) =>
        _flushQuiesceCoordinator.WaitForIdleAsync(IsOwnerIdleUnderLock, cancellationToken);

    private void ResumeWrites() => _flushQuiesceCoordinator.ResumeWrites();

    private void BeginBatch() =>
        _flushQuiesceCoordinator.BeginOwnerActivity(() => _activeBatchOperations++);

    private void BeginDequeuedMessage() =>
        _flushQuiesceCoordinator.BeginOwnerActivity(() => _activeDequeuedMessages++);

    private void EndDequeuedMessage() =>
        _flushQuiesceCoordinator.EndOwnerActivity(
            () => _activeDequeuedMessages--,
            IsOwnerIdleUnderLock
        );

    private void EndBatch() =>
        _flushQuiesceCoordinator.EndOwnerActivity(
            () => _activeBatchOperations--,
            IsOwnerIdleUnderLock
        );

    private bool IsFlushPending() => _flushQuiesceCoordinator.IsFlushPending();

    private bool IsOwnerIdleUnderLock() =>
        _activeDequeuedMessages == 0 && _activeBatchOperations == 0 && _channel.Reader.Count == 0;

    private void RegisterBatchDelayCancellationSource(CancellationTokenSource source)
    {
        lock (_batchDelayStateLock)
            _batchDelayCancellationSource = source;
    }

    private void ClearBatchDelayCancellationSource(CancellationTokenSource source)
    {
        lock (_batchDelayStateLock)
        {
            if (ReferenceEquals(_batchDelayCancellationSource, source))
                _batchDelayCancellationSource = null;
        }
    }

    private void CancelBatchDelayWait()
    {
        CancellationTokenSource? source;

        lock (_batchDelayStateLock)
        {
            source = _batchDelayCancellationSource;
            _batchDelayCancellationSource = null;
        }

        if (source is null)
            return;

        try
        {
            source.Cancel();
        }
        catch (ObjectDisposedException)
        {
            // Ignore cancellation races with completed batch-delay waits.
        }
    }
}
