namespace PicoDI.Sample.Host;

public sealed class StartupHostedSvc(IGreeter greeter) : IHostedSvc
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        Console.WriteLine(greeter.Greet("PicoDI Host"));
        Console.WriteLine("  StartupHostedSvc: container is ready.\n");
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        Console.WriteLine("  StartupHostedSvc: shutting down...");
        return Task.CompletedTask;
    }
}
