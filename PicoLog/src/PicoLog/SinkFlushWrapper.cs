namespace PicoLog;

internal sealed class SinkFlushWrapper : IFlushableLogSink
{
    private readonly ILogSink _inner;
    private readonly FlushQuiesceCoordinator _coordinator = new();

    public SinkFlushWrapper(ILogSink inner)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
    }

    public async Task WriteAsync(LogEntry entry, CancellationToken cancellationToken = default)
    {
        await _coordinator.EnterWriteOperationAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            await _inner.WriteAsync(entry, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _coordinator.ExitWriteOperation();
        }
    }

    public async ValueTask FlushAsync(CancellationToken cancellationToken = default)
    {
        await _coordinator.BlockWritesAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            await _coordinator
                .WaitForIdleAsync(IsOwnerIdleUnderLock, cancellationToken)
                .ConfigureAwait(false);

            cancellationToken.ThrowIfCancellationRequested();

            await ((IFlushableLogSink)_inner).FlushAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _coordinator.ResumeWrites();
        }
    }

    public ValueTask DisposeAsync() => _inner.DisposeAsync();

    private bool IsOwnerIdleUnderLock() => true;
}
