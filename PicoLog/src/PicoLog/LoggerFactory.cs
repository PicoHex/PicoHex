namespace PicoLog;

public sealed class LoggerFactory : IFlushableLoggerFactory
{
    private readonly Lock _registrationsLock = new();
    private readonly SemaphoreSlim _flushDisposeLock = new(1, 1);
    private readonly Dictionary<string, LoggerRegistration> _registrations =
        new(StringComparer.Ordinal);
    private readonly LoggerFactoryRuntime _runtime;
    private int _disposeState;

    public LoggerFactory(IEnumerable<ILogSink> sinks, LoggerFactoryOptions? options = null)
    {
        ILogSink[] sinks1 = sinks?.ToArray() ?? throw new ArgumentNullException(nameof(sinks));
        _runtime = new LoggerFactoryRuntime(sinks1, options);
    }

    public LogLevel MinLevel
    {
        get => _runtime.MinLevel;
        set => _runtime.MinLevel = value;
    }

    public ILogger CreateLogger(string categoryName)
    {
        ObjectDisposedException.ThrowIf(!_runtime.IsAcceptingWrites, this);
        ArgumentException.ThrowIfNullOrWhiteSpace(categoryName);

        lock (_registrationsLock)
        {
            ObjectDisposedException.ThrowIf(!_runtime.IsAcceptingWrites, this);

            if (_registrations.TryGetValue(categoryName, out var existingRegistration))
                return existingRegistration.Logger;

            var pipeline = new CategoryPipeline(categoryName, _runtime);
            var logger = new InternalLogger(categoryName, _runtime, pipeline);
            var registration = new LoggerRegistration(logger, pipeline);
            _registrations.Add(categoryName, registration);
            return logger;
        }
    }

    public async ValueTask FlushAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposeState != 0, this);

        await _flushDisposeLock.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            ObjectDisposedException.ThrowIf(!_runtime.IsAcceptingWrites, this);
            LoggerRegistration[] registrations;
            List<Exception>? exceptions = null;

            lock (_registrationsLock)
            {
                registrations =  [.. _registrations.Values];
            }

            Task[] pipelineFlushTasks = registrations
                .Select(
                    registration => registration.Pipeline.FlushAsync(cancellationToken).AsTask()
                )
                .ToArray();

            if (pipelineFlushTasks.Length != 0)
            {
                Task whenAll = Task.WhenAll(pipelineFlushTasks);

                try
                {
                    await whenAll.WaitAsync(cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    CollectCompletedTaskExceptions(pipelineFlushTasks, ref exceptions);
                    throw;
                }
                catch when (whenAll.Exception is not null)
                {
                    (exceptions ??=  []).AddRange(whenAll.Exception.Flatten().InnerExceptions);
                }
            }

            foreach (var sink in _runtime.Sinks)
            {
                if (sink is not IFlushableLogSink flushableSink)
                    continue;

                try
                {
                    await flushableSink.FlushAsync(cancellationToken).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    (exceptions ??=  []).Add(ex);
                }
            }

            if (exceptions is { Count: > 0 })
                throw new AggregateException(exceptions);
        }
        finally
        {
            _flushDisposeLock.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposeState, 1) != 0)
            return;

        List<Exception>? exceptions = null;
        var drainStopwatch = Stopwatch.StartNew();

        await _flushDisposeLock.WaitAsync().ConfigureAwait(false);

        try
        {
            LoggerRegistration[] registrations;

            lock (_registrationsLock)
            {
                if (!_runtime.TryBeginShutdown())
                    return;

                registrations =  [.. _registrations.Values];
                _registrations.Clear();
            }

            foreach (var registration in registrations)
            {
                try
                {
                    await registration.Pipeline.DisposeAsync().ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    (exceptions ??=  []).Add(ex);
                }
            }

            PicoLogMetrics.RecordShutdownDrainDuration(drainStopwatch.Elapsed);

            foreach (ILogSink sink in _runtime.Sinks)
            {
                try
                {
                    await sink.DisposeAsync().ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    (exceptions ??=  []).Add(ex);
                }
            }
        }
        finally
        {
            // Release instead of dispose: concurrent FlushAsync calls may still
            // be waiting on this semaphore after the TOCTOU dispose-state check.
            // The semaphore holds no unmanaged resources (only WaitAsync is used,
            // so no WaitHandle is created) and will be reclaimed by the GC.
            try
            {
                _flushDisposeLock.Release();
            }
            catch (ObjectDisposedException)
            {
                // Semaphore was already disposed by another path.
            }
        }

        if (exceptions is { Count: > 0 })
            throw new AggregateException(exceptions);
    }

    private static void CollectCompletedTaskExceptions(
        Task[] tasks,
        ref List<Exception>? exceptions
    )
    {
        foreach (var task in tasks)
        {
            if (task is { IsCompletedSuccessfully: false, IsFaulted: true })
                (exceptions ??=  []).AddRange(task.Exception!.Flatten().InnerExceptions);
        }
    }

    private sealed class LoggerRegistration(InternalLogger logger, CategoryPipeline pipeline)
    {
        public InternalLogger Logger { get; } = logger;

        public CategoryPipeline Pipeline { get; } = pipeline;
    }
}
