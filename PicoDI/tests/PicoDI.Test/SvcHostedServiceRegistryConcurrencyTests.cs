namespace PicoDI.Test;

public class SvcHostedServiceRegistryConcurrencyTests
{
    // One generic definition provides an unbounded supply of distinct closed
    // types for the writer threads.
    private sealed class Seed<T1, T2, T3>;

    private static readonly Type[] SeedArguments =
    [
        typeof(byte),
        typeof(sbyte),
        typeof(short),
        typeof(ushort),
        typeof(int),
        typeof(uint),
        typeof(long),
        typeof(ulong),
        typeof(float),
        typeof(double),
        typeof(decimal),
        typeof(char),
        typeof(string),
        typeof(object),
    ];

    [NotInParallel]
    [Test]
    public async Task ConcurrentRegisterAndContains_DoesNotCorruptRegistry()
    {
        // Regression: the registry backed a plain HashSet with no
        // synchronization. Concurrent Register (Add) and Contains (read with
        // version check) corrupted the set and threw
        // InvalidOperationException("concurrent update") — observed in
        // HostBuilderTests when parallel test classes registered hosted
        // services while others built containers.
        var seedDefinition = typeof(Seed<,,>);

        var start = new ManualResetEventSlim(false);
        var stop = new CancellationTokenSource();
        var failures = new System.Collections.Concurrent.ConcurrentQueue<Exception>();

        // Builds a distinct closed Seed<,,> type for every (n, depth) pair by
        // encoding n in a mixed-radix nest — an unbounded supply of types
        // that have never been registered, so every Register() really adds
        // and bumps the internal version.
        Type BuildUniqueType(long n, int depth)
        {
            var leaf = SeedArguments[n % SeedArguments.Length];
            if (depth == 0)
                return leaf;
            var a = SeedArguments[(n / 14) % SeedArguments.Length];
            var b = SeedArguments[(n / 196) % SeedArguments.Length];
            var nested = BuildUniqueType(n / 2744, depth - 1);
            return seedDefinition.MakeGenericType(a, b, nested);
        }

        // Dedicated threads force real parallelism (the thread pool may
        // serialize short CPU-bound bursts), and a timed loop guarantees the
        // writers and readers overlap for the whole duration.
        var writerCounter = 0L;
        var threads = new List<Thread>();
        for (var i = 0; i < 4; i++)
        {
            threads.Add(
                new Thread(() =>
                {
                    start.Wait();
                    try
                    {
                        while (!stop.IsCancellationRequested)
                        {
                            var n = Interlocked.Increment(ref writerCounter);
                            SvcHostedServiceRegistry.Register(BuildUniqueType(n, 3));
                        }
                    }
                    catch (Exception ex)
                    {
                        failures.Enqueue(ex);
                    }
                })
            );
        }

        for (var i = 0; i < 4; i++)
        {
            threads.Add(
                new Thread(() =>
                {
                    start.Wait();
                    try
                    {
                        while (!stop.IsCancellationRequested)
                            _ = SvcHostedServiceRegistry.Contains(typeof(IHostedSvc));
                    }
                    catch (Exception ex)
                    {
                        failures.Enqueue(ex);
                    }
                })
            );
        }

        foreach (var thread in threads)
            thread.Start();

        start.Set();
        stop.CancelAfter(TimeSpan.FromSeconds(3));
        foreach (var thread in threads)
            thread.Join(TimeSpan.FromSeconds(10));

        if (failures.TryDequeue(out var failure))
            throw new InvalidOperationException("Registry corruption detected.", failure);
    }
}
