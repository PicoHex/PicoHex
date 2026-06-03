namespace PicoMediator.Tests;

public class ConcurrencyStressTests
{
    public record StressPing(int Id) : IRequest<string>;

    public sealed class StressPingHandler : IRequestHandler<StressPing, string>
    {
        public int CallCount;

        public ValueTask<string> Handle(StressPing r, CancellationToken ct)
        {
            Interlocked.Increment(ref CallCount);
            return ValueTask.FromResult($"pong-{r.Id}");
        }
    }

    public record StressNotif(int Id) : INotification;

    public sealed class StressNotifHandler(List<int> received) : INotificationHandler<StressNotif>
    {
        public ValueTask Handle(StressNotif n, CancellationToken ct)
        {
            lock (received)
                received.Add(n.Id);
            return ValueTask.CompletedTask;
        }
    }

    [Test]
    public async Task Send_ManyConcurrent_AllReturnCorrectResults()
    {
        var handler = new StressPingHandler();
        var container = new SvcContainer(autoConfigureFromGenerator: false);
        container.RegisterSingle<IRequestHandler<StressPing, string>>(handler);
        container.AddPicoMediator();
        container.Build();

        await using var scope = container.CreateScope();
        var mediator = scope.GetService<IMediator>();

        var tasks = new Task<string>[100];
        for (var i = 0; i < 100; i++)
        {
            var id = i;
            tasks[i] = Task.Run(() =>
                mediator.Send<StressPing, string>(new StressPing(id)).AsTask()
            );
        }

        var results = await Task.WhenAll(tasks);

        await Assert.That(results.Length).IsEqualTo(100);
        await Assert.That(handler.CallCount).IsEqualTo(100);
        for (var i = 0; i < 100; i++)
            await Assert.That(results).Contains($"pong-{i}");
    }

    [Test]
    public async Task Publish_ManySubscribers_AllNotified()
    {
        const int subscriberCount = 50;
        var received = new List<int>();
        var container = new SvcContainer(autoConfigureFromGenerator: false);

        for (var i = 0; i < subscriberCount; i++)
            container.RegisterSingle<INotificationHandler<StressNotif>>(
                new StressNotifHandler(received)
            );

        container.AddPicoMediator();
        container.Build();

        await using var scope = container.CreateScope();
        var mediator = scope.GetService<IMediator>();

        await mediator.Publish(new StressNotif(42));

        await Assert.That(received.Count).IsEqualTo(subscriberCount);
    }
}
