namespace PicoDI.Abs;

/// <summary>
/// Base class for a background service that runs continuously until stopped.
/// Implements the template method pattern via <see cref="ExecuteAsync"/>.
/// </summary>
public abstract class BackgroundSvc : IHostedSvc, IAsyncDisposable
{
    private CancellationTokenSource? _stoppingCts;
    private Task? _executingTask;
    private int _disposed;

    /// <summary>
    /// Executes the background operation. Called by <see cref="StartAsync"/>.
    /// Implementations should observe the <paramref name="stoppingToken"/>
    /// and exit gracefully when cancellation is requested.
    /// </summary>
    /// <param name="stoppingToken">A cancellation token that signals when the service should stop.</param>
    /// <returns>A task that represents the long-running background operation.</returns>
    protected abstract Task ExecuteAsync(CancellationToken stoppingToken);

    /// <summary>
    /// Initiates the background service. <see cref="ExecuteAsync"/> is invoked and the returned
    /// task indicates whether startup completed synchronously.
    /// </summary>
    /// <remarks>
    /// When <see cref="ExecuteAsync"/> starts asynchronously (does not complete inline), this method
    /// returns <see cref="Task.CompletedTask"/> — the background work is fire-and-forget from the
    /// caller's perspective and runs until <see cref="StopAsync"/> or <see cref="DisposeAsync"/> is
    /// called. When <see cref="ExecuteAsync"/> completes synchronously (e.g., it faults or returns
    /// immediately), that task is returned directly so the caller observes the outcome.
    /// <para />
    /// The returned task therefore represents only the startup phase, not the full background
    /// lifecycle. Use <see cref="StopAsync"/> to await the graceful completion of the background
    /// operation.
    /// </remarks>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel startup.</param>
    /// <returns>
    /// <see cref="Task.CompletedTask"/> when startup completed synchronously and the background
    /// work continues asynchronously; otherwise the faulted/cancelled/succeeded task from
    /// <see cref="ExecuteAsync"/> if it completed inline.
    /// </returns>
    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (Volatile.Read(ref _disposed) != 0)
            throw new ObjectDisposedException(nameof(BackgroundSvc));

        var cts = new CancellationTokenSource();

        if (Interlocked.CompareExchange(ref _stoppingCts, cts, null) is not null)
        {
            cts.Dispose();
            throw new InvalidOperationException("The background service has already been started.");
        }

        Volatile.Write(ref _executingTask, ExecuteWithErrorHandlingAsync(cts.Token));

        // Fire-and-forget: if the task hasn't completed inline, return Task.CompletedTask
        // to signal that startup is done. The background work continues on _executingTask.
        return _executingTask is { IsCompleted: true } ? _executingTask : Task.CompletedTask;
    }

    /// <summary>
    /// Signals the service to stop and waits for it to complete.
    /// </summary>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the shutdown wait.</param>
    public async Task StopAsync(CancellationToken cancellationToken)
    {
        var cts = _stoppingCts;
        if (cts is null)
            return;

        try
        {
            cts.Cancel();
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"Error cancelling background service: {ex}");
        }

        var task = Volatile.Read(ref _executingTask);
        if (task is null)
            return;

        try
        {
            // Manually replicate Task.WaitAsync(ct) for netstandard2.0: register a
            // continuation on the caller's token that completes a TaskCompletionSource,
            // then race it against the executing task. The registration is ALWAYS
            // disposed on exit, so no callback is leaked on the caller's CTS when
            // the executing task finishes first (Bug 8). The previous
            // Task.WhenAny(task, Task.Delay(Infinite, ct)) pattern leaked the delay's
            // registration until the source CTS was cancelled or disposed.
            if (cancellationToken.CanBeCanceled)
            {
                var tcs = new TaskCompletionSource<bool>(
                    TaskCreationOptions.RunContinuationsAsynchronously
                );
                using (
                    cancellationToken.Register(
                        static state => ((TaskCompletionSource<bool>)state!).TrySetResult(true),
                        tcs
                    )
                )
                {
                    await Task.WhenAny(task, tcs.Task).ConfigureAwait(false);
                }

                // WhenAny does not propagate exceptions from the inner task.
                // If ExecuteAsync faulted (not cancelled), re-await so callers
                // observe the failure rather than silently swallowing it.
                if (task.IsFaulted)
                    await task.ConfigureAwait(false);
            }
            else
            {
                await task.ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
            // Cancellation was requested — stop waiting for the background task.
            // If the task itself faulted (not cancelled), log it so callers don't
            // silently lose the failure.
            if (task.IsFaulted)
            {
                Trace.WriteLine(
                    $"Background service faulted while waiting for stop: {task.Exception?.InnerException}"
                );
            }
        }
    }

    /// <summary>
    /// Releases resources used by the background service.
    /// Cancels the stopping token, awaits the executing task, then disposes the CTS.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 1)
            return;

        var cts = Interlocked.Exchange(ref _stoppingCts, null);
        if (cts is not null)
        {
            try
            {
                cts.Cancel();
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"Error cancelling background service: {ex}");
            }
        }

        if (Volatile.Read(ref _executingTask) is { } task)
        {
            try
            {
                await task.ConfigureAwait(false);
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                Trace.WriteLine($"Background service faulted during disposal: {ex}");
            }
        }

        cts?.Dispose();
    }

    /// <summary>
    /// Wraps <see cref="ExecuteAsync"/> with exception tracing.
    /// Exceptions are logged via <see cref="Trace.WriteLine"/> and re-thrown.
    /// </summary>
    private async Task ExecuteWithErrorHandlingAsync(CancellationToken stoppingToken)
    {
        try
        {
            await ExecuteAsync(stoppingToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"Error in background service: {ex}");
            throw;
        }
    }
}
