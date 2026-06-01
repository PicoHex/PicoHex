namespace PicoMediator.Tests;

public class SendDispatchTests
{
    // ── Request types ──

    public record CreateOrder(string Item, int Qty) : IRequest<OrderResult>;

    public record OrderResult(Guid Id);

    public record DeleteOrder(Guid Id) : IRequest<PicoDI.Abs.VoidResult>;

    // ── Handlers ──

    public sealed class CreateOrderHandler : IRequestHandler<CreateOrder, OrderResult>
    {
        public ValueTask<OrderResult> Handle(CreateOrder r, CancellationToken ct) =>
            ValueTask.FromResult(new OrderResult(Guid.NewGuid()));
    }

    public sealed class DeleteOrderHandler : IRequestHandler<DeleteOrder, PicoDI.Abs.VoidResult>
    {
        public int CallCount;

        public async ValueTask<PicoDI.Abs.VoidResult> Handle(DeleteOrder r, CancellationToken ct)
        {
            Interlocked.Increment(ref CallCount);
            await Task.Yield();
            return default;
        }
    }

    // ── Send tests ──

    [Test]
    public async Task Send_WithRegisteredHandler_ReturnsResponse()
    {
        var container = new SvcContainer(autoConfigureFromGenerator: false);
        container.RegisterTransient<IRequestHandler<CreateOrder, OrderResult>>(
            _ => new CreateOrderHandler()
        );
        container.Build();
        await using var scope = container.CreateScope();

        var mediator = new Mediator(scope);
        var result = await mediator.Send<CreateOrder, OrderResult>(new CreateOrder("book", 3));

        await Assert.That(result).IsNotNull();
        await Assert.That(result.Id).IsNotEqualTo(Guid.Empty);
    }

    [Test]
    public async Task Send_VoidResult_CompletesSuccessfully()
    {
        var container = new SvcContainer(autoConfigureFromGenerator: false);
        var handler = new DeleteOrderHandler();
        container.RegisterSingle<IRequestHandler<DeleteOrder, PicoDI.Abs.VoidResult>>(handler);
        container.Build();
        await using var scope = container.CreateScope();

        var mediator = new Mediator(scope);
        await mediator.Send<DeleteOrder, PicoDI.Abs.VoidResult>(new DeleteOrder(Guid.NewGuid()));

        await Assert.That(handler.CallCount).IsEqualTo(1);
    }

    [Test]
    public async Task Send_NoHandler_Throws()
    {
        var container = new SvcContainer(autoConfigureFromGenerator: false);
        container.Build();
        await using var scope = container.CreateScope();
        var mediator = new Mediator(scope);

        await Assert.ThrowsAsync(
            async () => await mediator.Send<CreateOrder, OrderResult>(new CreateOrder("x", 1))
        );
    }
}
