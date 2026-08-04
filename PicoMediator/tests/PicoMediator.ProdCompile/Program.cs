using PicoDI;
using PicoDI.Abs;
using PicoMediator;
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
    public ValueTask Handle(OrderPaid e, CancellationToken ct) => ValueTask.CompletedTask;
}

public static class Program
{
    public static async Task Main()
    {
        var container = new SvcContainer();
        container.RegisterTransient<ICommandHandler<Ping, string>>(_ => new PingHandler());
        container.RegisterSingle<ISubscriber<OrderPaid>>(new OrderPaidSubscriber());
        container.AddPicoMediator();
        container.Build();

        await using var scope = container.CreateScope();
        var mediator = (IMediator)scope.GetService(typeof(IMediator));

        var r = await mediator.Send<Ping, string>(new Ping("hi"));
        if (r != "pong:hi")
            throw new Exception($"Send failed: '{r}'");

        await mediator.Publish(new OrderPaid(42));
        Console.WriteLine("ProdCompile smoke OK");
    }
}
