namespace PicoDI.Sample.Host;

public sealed class PeriodicBackgroundSvc(IGreeter greeter) : BackgroundSvc
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        Console.WriteLine("  PeriodicBackgroundSvc: background work started.\n");

        while (!stoppingToken.IsCancellationRequested)
        {
            Console.WriteLine(
                $"[{DateTime.Now:HH:mm:ss}] {greeter.Greet("Background")}"
            );
            await Task.Delay(2000, stoppingToken);
        }
    }
}
