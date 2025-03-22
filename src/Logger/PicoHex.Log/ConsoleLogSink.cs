﻿namespace PicoHex.Log;

public class ConsoleLogSink : ILogSink
{
    private readonly Channel<LogEntry> _channel = Channel.CreateUnbounded<LogEntry>();
    private readonly ILogFormatter _formatter;
    private readonly Task _processingTask;

    public ConsoleLogSink(ILogFormatter formatter)
    {
        _formatter = formatter;
        _processingTask = Task.Run(ProcessEntries);
    }

    private async Task ProcessEntries()
    {
        await foreach (var entry in _channel.Reader.ReadAllAsync())
        {
            try
            {
                var formatted = _formatter.Format(entry);
                Console.WriteLine(formatted);
            }
            catch
            { /* 确保主线程不受影响 */
            }
        }
    }

    public async ValueTask WriteAsync(
        LogEntry entry,
        CancellationToken cancellationToken = default
    ) => await _channel.Writer.WriteAsync(entry, cancellationToken);

    public void Dispose()
    {
        _channel.Writer.Complete();
        _processingTask.Wait();
    }
}
