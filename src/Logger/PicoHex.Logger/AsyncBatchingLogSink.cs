using System.Collections.Concurrent;

namespace PicoHex.Logger;

public class AsyncBatchingLogSink : ILogSink, IDisposable
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
                if (_queue.TryTake(out var message, 1000))
                {
                    batch.Add(message);
                    if (batch.Count >= batchSize)
                    {
                        await innerSink.WriteAsync(string.Join(Environment.NewLine, batch));
                        batch.Clear();
                    }
                }
            }
        });
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
}
