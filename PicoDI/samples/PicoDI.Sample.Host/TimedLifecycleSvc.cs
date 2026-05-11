namespace PicoDI.Sample.Host;

public sealed class TimedLifecycleSvc(IClock clock) : IHostedLifecycleSvc
{
    public Task StartingAsync(CancellationToken cancellationToken)
    {
        Console.WriteLine($"  [{clock.Now:T}] TimedLifecycleSvc.StartingAsync");
        return Task.CompletedTask;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        Console.WriteLine($"  [{clock.Now:T}] TimedLifecycleSvc.StartAsync");
        return Task.CompletedTask;
    }

    public Task StartedAsync(CancellationToken cancellationToken)
    {
        Console.WriteLine($"  [{clock.Now:T}] TimedLifecycleSvc.StartedAsync");
        return Task.CompletedTask;
    }

    public Task StoppingAsync(CancellationToken cancellationToken)
    {
        Console.WriteLine($"  [{clock.Now:T}] TimedLifecycleSvc.StoppingAsync");
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        Console.WriteLine($"  [{clock.Now:T}] TimedLifecycleSvc.StopAsync");
        return Task.CompletedTask;
    }

    public Task StoppedAsync(CancellationToken cancellationToken)
    {
        Console.WriteLine($"  [{clock.Now:T}] TimedLifecycleSvc.StoppedAsync");
        return Task.CompletedTask;
    }
}
