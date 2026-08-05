using PicoDI;
using PicoMediator.Abs;
using PicoMediator.DI;

// Production-shaped assembly: no global using PicoDI.Abs, no friend privileges.
// If the generator emits non-public or using-dependent code, THIS PROJECT FAILS TO COMPILE.

public record Ping(string Message) : ICommand<string>;

public sealed class PingHandler : ICommandHandler<Ping, string>
{
    public ValueTask<string> Handle(Ping c, CancellationToken ct) => new($"pong:{c.Message}");
}

public record OrderPaid(int OrderId) : IEvent;

public sealed class OrderPaidSubscriber : ISubscriber<OrderPaid>
{
    public static int Calls;

    public ValueTask Handle(OrderPaid e, CancellationToken ct)
    {
        Interlocked.Increment(ref Calls);
        return ValueTask.CompletedTask;
    }
}

public static class Program
{
    public static async Task Main()
    {
        var container = new SvcContainer();
        container.AddPicoMediator();
        container.Build();

        await using var scope = container.CreateScope();
        var mediator = (IMediator)scope.GetService(typeof(IMediator));

        // Auto-subscription: PingHandler and OrderPaidSubscriber were registered
        // by the generated configurator — no manual registration.
        var r = await mediator.Send<Ping, string>(new Ping("hi"));
        if (r != "pong:hi")
            throw new Exception($"Send failed: '{r}'");

        await mediator.Publish(new OrderPaid(42));

        // Base-typed publish via generated bridge (no new API):
        OrderPaidSubscriber.Calls = 0;
        IReadOnlyList<IEvent> events = [new OrderPaid(1), new OrderPaid(2)];
        foreach (var e in events)
            await mediator.Publish(e);
        if (OrderPaidSubscriber.Calls != 2)
            throw new Exception(
                $"Base-typed publish failed: {OrderPaidSubscriber.Calls}/2 delivered"
            );

        Console.WriteLine("ProdCompile smoke OK");
    }
}
