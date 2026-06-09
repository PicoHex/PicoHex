namespace PicoLog;

public sealed class FileSink : ILogSink, IFlushableLogSink
{
    private readonly Channel<string> _channel;
    private readonly ILogFormatter _formatter;
    private readonly FileSinkOptions _options;
    private readonly string _baseFilePath;
    private readonly Lock _fileLock = new();
    private StreamWriter _writer;
    private readonly Task _processingTask;
    private readonly SemaphoreSlim _flushSemaphore = new(1, 1);
    private readonly FlushQuiesceCoordinator _flushQuiesceCoordinator = new();
    private int _disposeState;
    private int _activeDequeuedMessages;
    private int _activeBatchOperations;
    private int _rotationIndex;
    private long _lastRotationTimestamp;
    private Exception? _processingException;

    public FileSink(ILogFormatter formatter, string filePath = FileSinkOptions.DefaultFilePath)
        : this(formatter, new FileSinkOptions { FilePath = filePath }) { }

    public FileSink(ILogFormatter formatter, FileSinkOptions options)
    {
        _formatter = formatter ?? throw new ArgumentNullException(nameof(formatter));
        _options = (
            options ?? throw new ArgumentNullException(nameof(options))
        ).CreateValidatedCopy();

        _baseFilePath = Path.GetFullPath(_options.FilePath);
        var directory =
            Path.GetDirectoryName(_baseFilePath)
            ?? throw new ArgumentException(
                $"File path must not be a root directory: '{_options.FilePath}'",
                nameof(options)
            );

        if (!Directory.Exists(directory))
            Directory.CreateDirectory(directory);

        _channel = Channel.CreateBounded<string>(
            new BoundedChannelOptions(_options.QueueCapacity)
            {
                FullMode = BoundedChannelFullMode.Wait,
                SingleReader = true,
            }
        );

        var fileStream = new FileStream(
            _baseFilePath,
            FileMode.OpenOrCreate,
            FileAccess.Write,
            FileShare.Read,
            bufferSize: 4096,
            useAsync: true
        );

        _writer = new StreamWriter(fileStream, Encoding.UTF8);
        _lastRotationTimestamp = DateTime.UtcNow.Ticks;
        _processingTask = ProcessWritesAsync();
    }

    public async Task WriteAsync(LogEntry entry, CancellationToken cancellationToken = default)
    {
        if (Volatile.Read(ref _disposeState) != 0)
        {
            PicoLogMetrics.RecordRejectedAfterShutdown();
            return;
        }

        var message = _formatter.Format(entry);

        try
        {
            await _channel.Writer.WriteAsync(message, cancellationToken).ConfigureAwait(false);
        }
        catch (ChannelClosedException)
        {
            // Channel closed means the sink is shutting down.
            // Writes arriving after channel completion are expected
            // and silently discarded — the entry was already in flight
            // when disposal began.
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

    private async Task ProcessWritesAsync()
    {
        try
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
        catch (Exception ex)
        {
            _processingException = ex;
        }
    }

    private async ValueTask DrainBatchAsync(List<string> batch)
    {
        if (_options.AllowFlushInterrupt)
        {
            while (batch.Count < _options.BatchSize)
            {
                if (IsFlushPending())
                    break;

                // Synchronous read — no timer, no CancellationTokenSource, no Task.WhenAny.
                // If a message is already available, add it and keep filling the batch.
                // If not, flush immediately. This avoids the ARM64 timer reliability issue
                // entirely while preserving batching under load (messages arrive faster than
                // the processing loop can drain).
                if (!_channel.Reader.TryRead(out var message))
                    break;

                batch.Add(message);
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

        await RotateIfNeededAsync().ConfigureAwait(false);
    }

    private async ValueTask RotateIfNeededAsync()
    {
        // Check size-based rotation
        bool needsSizeRotation =
            _options.MaxFileSizeBytes > 0 && _writer.BaseStream.Length >= _options.MaxFileSizeBytes;

        // Check time-based rotation
        bool needsTimeRotation =
            _options.RotationInterval > TimeSpan.Zero
            && (DateTime.UtcNow.Ticks - Volatile.Read(ref _lastRotationTimestamp))
                >= _options.RotationInterval.Ticks;

        if (!needsSizeRotation && !needsTimeRotation)
            return;

        lock (_fileLock)
        {
            // Re-check under lock
            bool sizeReady =
                _options.MaxFileSizeBytes > 0
                && _writer.BaseStream.Length >= _options.MaxFileSizeBytes;
            bool timeReady =
                _options.RotationInterval > TimeSpan.Zero
                && (DateTime.UtcNow.Ticks - Volatile.Read(ref _lastRotationTimestamp))
                    >= _options.RotationInterval.Ticks;

            if (!sizeReady && !timeReady)
                return;

            _writer.Dispose();

            var rotatedPath = GetRotatedFilePath();
            File.Move(_baseFilePath, rotatedPath, overwrite: true);
            CleanUpOldFiles();

            var fs = new FileStream(
                _baseFilePath,
                FileMode.OpenOrCreate,
                FileAccess.Write,
                FileShare.Read,
                bufferSize: 4096,
                useAsync: true
            );
            _writer = new StreamWriter(fs, Encoding.UTF8);
            _rotationIndex++;
        }
    }

    private string GetRotatedFilePath()
    {
        var dir = Path.GetDirectoryName(_baseFilePath)!;
        var name = Path.GetFileNameWithoutExtension(_baseFilePath);
        var ext = Path.GetExtension(_baseFilePath);
        return Path.Combine(dir, $"{name}.{_rotationIndex + 1}{ext}");
    }

    private void CleanUpOldFiles()
    {
        if (_options.MaxRetainedFiles <= 0)
            return;

        var dir = Path.GetDirectoryName(_baseFilePath)!;
        var name = Path.GetFileNameWithoutExtension(_baseFilePath);
        var ext = Path.GetExtension(_baseFilePath);
        var rotatedFiles = Directory
            .GetFiles(dir, $"{name}.*{ext}")
            .OrderBy(f =>
            {
                var fname = Path.GetFileNameWithoutExtension(f);
                var suffix = fname.Substring(name.Length + 1);
                return int.TryParse(suffix, out var n) ? n : 0;
            })
            .ToList();

        while (rotatedFiles.Count > _options.MaxRetainedFiles)
        {
            try
            {
                File.Delete(rotatedFiles[0]);
            }
            catch { }
            rotatedFiles.RemoveAt(0);
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposeState, 1) != 0)
            return;

        await _flushSemaphore.WaitAsync().ConfigureAwait(false);

        try
        {
            _channel.Writer.TryComplete();
            await ShutdownAsync().ConfigureAwait(false);
        }
        finally
        {
            _flushSemaphore.Release();
        }
    }

    private async Task ShutdownAsync()
    {
        Exception? processingException = null;

        try
        {
            await _processingTask.ConfigureAwait(false);
            processingException = _processingException;
        }
        catch (Exception ex)
        {
            processingException = ex;
        }

        try
        {
            await _writer.FlushAsync().ConfigureAwait(false);
        }
        catch (Exception ex) when (processingException is null)
        {
            processingException = ex;
        }
        finally
        {
            await _writer.DisposeAsync().ConfigureAwait(false);
        }

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
}
