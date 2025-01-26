namespace PicoHex.Logger;

public class AsyncBatchingLogSink : ILogSink
{
    private readonly BlockingCollection<string> _queue = new();
    private readonly CancellationTokenSource _cts = new();
    private readonly Task _processingTask;

    public AsyncBatchingLogSink(ILogSink innerSink, int batchSize = 100)
    {
        _processingTask = Task.Run(async () =>
        {
            var batch = new List<string>(batchSize);
            while (!_cts.IsCancellationRequested)
            {
                if (!_queue.TryTake(out var message, 1000))
                    continue;
                batch.Add(message);
                if (batch.Count < batchSize)
                    continue;
                await innerSink.WriteAsync(string.Join(Environment.NewLine, batch));
                batch.Clear();
            }
        });
    }

    public void Write(string formattedMessage)
    {
        throw new NotImplementedException();
    }

    public ValueTask WriteAsync(string formattedMessage)
    {
        _queue.Add(formattedMessage);
        return ValueTask.CompletedTask;
    }

    public void Dispose()
    {
        _cts.Cancel();
        _processingTask.Wait();
    }

    public async ValueTask DisposeAsync()
    {
        await CastAndDispose(_queue);
        await CastAndDispose(_cts);
        await CastAndDispose(_processingTask);

        return;

        static async ValueTask CastAndDispose(IDisposable resource)
        {
            if (resource is IAsyncDisposable resourceAsyncDisposable)
                await resourceAsyncDisposable.DisposeAsync();
            else
                resource.Dispose();
        }
    }
}
