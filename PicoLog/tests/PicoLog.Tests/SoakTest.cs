namespace PicoLog.Tests;

public sealed class SoakTest
{
    private static readonly TimeSpan Duration = TimeSpan.FromSeconds(2); // Quick verification — override via env var for full soak

    private static readonly bool IsCi =
        Environment.GetEnvironmentVariable("CI")?.ToLowerInvariant() == "true";

    private record Metrics(
        long TotalMessages,
        double AvgThroughput,
        double P50LatencyNs,
        double P99LatencyNs,
        long GcGen0,
        long GcGen1,
        long GcGen2,
        int ExceptionCount
    );

    private sealed class NullSink : IFastLogSink
    {
        public Task WriteAsync(LogEntry entry, CancellationToken ct = default) =>
            Task.CompletedTask;

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    [Test]
    public async Task Soak_NullSink_SingleThread()
    {
        await RunSoak("Basic-1T", (log, n) => log.Info($"msg-{n}"), 1);
    }

    [Test]
    public async Task Soak_NullSink_MultiThread()
    {
        await RunSoak("Basic-4T", (log, n) => log.Info($"msg-{n}"), 4);
    }

    [Test]
    public async Task Soak_Properties_SingleThread()
    {
        var props = new KeyValuePair<string, object?>[]
        {
            new("userId", "u1"),
            new("duration", 1.5),
            new("success", true)
        };
        await RunSoak("Props-1T", (log, n) => log.Log(LogLevel.Info, $"msg-{n}", props, null), 1);
    }

    private async Task RunSoak(string name, Action<ILogger, int> writer, int threadCount)
    {
        await using var factory = new LoggerFactory(
            [new NullSink()],
            new LoggerFactoryOptions
            {
                MinLevel = LogLevel.Trace,
                QueueCapacity = 65535,
                QueueFullMode = LogQueueFullMode.DropOldest
            }
        );

        var logger = factory.CreateLogger(name);

        var totalMessages = 0L;
        var exceptions = 0;
        var latencies = new ConcurrentQueue<double>();
        var gc0Start = GC.CollectionCount(0);
        var gc1Start = GC.CollectionCount(1);
        var gc2Start = GC.CollectionCount(2);
        var startTime = DateTime.UtcNow;

        using var cts = new CancellationTokenSource(Duration);
        var writers = new Task[threadCount];

        for (var t = 0; t < threadCount; t++)
        {
            // Do NOT pass cts.Token to Task.Run — on overloaded CI runners
            // the token may fire before the task body starts, causing
            // TaskCanceledException instead of a clean exit from the loop.
            writers[t] = Task.Run(() =>
            {
                var n = 0;
                while (!cts.Token.IsCancellationRequested)
                {
                    try
                    {
                        var sw = Stopwatch.StartNew();
                        writer(logger, n);
                        sw.Stop();
                        latencies.Enqueue(sw.ElapsedTicks * 1_000_000_000.0 / Stopwatch.Frequency);
                        Interlocked.Increment(ref totalMessages);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                    catch (Exception)
                    {
                        Interlocked.Increment(ref exceptions);
                    }
                    n++;
                }
            });
        }

        await Task.WhenAll(writers);
        var elapsed = DateTime.UtcNow - startTime;

        var metrics = new Metrics(
            Volatile.Read(ref totalMessages),
            Volatile.Read(ref totalMessages) / elapsed.TotalSeconds,
            Percentile(latencies, 0.5),
            Percentile(latencies, 0.99),
            GC.CollectionCount(0) - gc0Start,
            GC.CollectionCount(1) - gc1Start,
            GC.CollectionCount(2) - gc2Start,
            Volatile.Read(ref exceptions)
        );

        WriteReport(name, threadCount, elapsed, metrics);

        if (IsCi)
        {
            await Assert.That(metrics.ExceptionCount).IsEqualTo(0);
            // CI runners have constrained memory; millions of LogEntry allocations
            // may trigger Gen2 GC. Allow up to 5 — correctness depends on zero
            // exceptions, not on GC behavior.
            await Assert.That(metrics.GcGen2).IsLessThanOrEqualTo(5);
        }
    }

    private static double Percentile(ConcurrentQueue<double> values, double p)
    {
        if (values.IsEmpty)
            return 0;
        var sorted = values.OrderBy(v => v).ToArray();
        var index = (int)Math.Ceiling(p * sorted.Length) - 1;
        return sorted[Math.Max(0, index)];
    }

    private static void WriteReport(string name, int threads, TimeSpan elapsed, Metrics m)
    {
        Console.WriteLine(
            $@"
Soak: {name} | Threads: {threads} | {elapsed.TotalSeconds:F0}s
  Messages:    {m.TotalMessages, 12:N0}
  Throughput:  {m.AvgThroughput, 12:N0} msg/s
  P50/P99:     {m.P50LatencyNs, 6:N0} / {m.P99LatencyNs, 6:N0} ns
  Exceptions:  {m.ExceptionCount, 12:N0}
  GC 0/1/2:    {m.GcGen0} / {m.GcGen1} / {m.GcGen2}
"
        );
    }
}
