namespace PicoHex.Logger.NG;

public class AsyncLogProcessor : IDisposable
{
    private readonly BlockingCollection<LogEntry> _logQueue = new();
    private readonly IEnumerable<ILogSink> _sinks;

    public AsyncLogProcessor(IEnumerable<ILogSink> sinks)
    {
        _sinks = sinks;
        StartProcessing();
    }

    private void StartProcessing()
    {
        Task.Run(async () =>
        {
            while (!_logQueue.IsCompleted)
            {
                if (!_logQueue.TryTake(out var entry))
                    continue;
                foreach (var sink in _sinks)
                {
                    await sink.EmitAsync(entry);
                }
            }
        });
    }

    public void Enqueue(LogEntry entry) => _logQueue.Add(entry);

    public void Dispose()
    {
        _logQueue.CompleteAdding();
        _logQueue.Dispose();
    }
}
