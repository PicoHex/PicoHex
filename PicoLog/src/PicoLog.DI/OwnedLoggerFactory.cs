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
        Task.Run(async () => await DisposeAsync().ConfigureAwait(false)).GetAwaiter().GetResult();
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
