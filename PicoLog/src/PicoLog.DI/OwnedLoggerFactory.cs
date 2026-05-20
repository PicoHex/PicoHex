namespace PicoLog.DI;

internal sealed class OwnedLoggerFactory(LoggerFactory innerFactory, IAsyncDisposable ownedScope)
    : ILoggerFactory,
        IFlushableLoggerFactory,
        IDisposable,
        IAsyncDisposable
{
    private int _disposeState;

    public ILogger CreateLogger(string categoryName) => innerFactory.CreateLogger(categoryName);

    public ValueTask FlushAsync(CancellationToken cancellationToken = default) =>
        innerFactory.FlushAsync(cancellationToken);

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposeState, 1) != 0)
            return;

        // innerFactory.Dispose() is fully synchronous (Thread.Join on dedicated
        // processing threads). No sync-over-async bridge → no pool starvation risk.
        // ownedScope (IAsyncDisposable) still needs a sync bridge, but disposing
        // innerFactory first frees all pool resources so the bridge won't deadlock.
        Exception? factoryException = null;

        try
        {
            innerFactory.Dispose();
        }
        catch (Exception ex)
        {
            factoryException = ex;
        }

        try
        {
            Task.Run(async () => await ownedScope.DisposeAsync().ConfigureAwait(false))
                .GetAwaiter()
                .GetResult();
        }
        catch (Exception scopeException)
        {
            if (factoryException is not null)
                throw new AggregateException(factoryException, scopeException);

            throw;
        }

        if (factoryException is not null)
            throw factoryException;
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposeState, 1) != 0)
            return;

        Exception? factoryException = null;
        Exception? scopeException = null;

        try
        {
            await innerFactory.DisposeAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            factoryException = ex;
        }

        try
        {
            await ownedScope.DisposeAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            scopeException = ex;
        }

        if (factoryException is not null && scopeException is not null)
            throw new AggregateException(factoryException, scopeException);

        if (factoryException is not null)
            throw factoryException;

        if (scopeException is not null)
            throw scopeException;
    }
}
