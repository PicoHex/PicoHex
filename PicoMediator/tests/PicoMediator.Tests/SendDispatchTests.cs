namespace PicoMediator.Tests;

[NotInParallel]
public class SendDispatchTests
{
    [Before(Test)]
    public void ClearSwitchesBeforeTest()
    {
        GeneratedDispatch.ClearSwitches();
    }

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
    public async Task Send_WithMismatchedGeneratedDispatch_ThrowsMeaningfulException()
    {
        // Bug: GeneratedDispatch.Send blindly casts object? to ValueTask<TResponse>.
        // If a registered switch returns a ValueTask of the wrong type (e.g. version
        // mismatch between generated code and runtime), the hard cast throws
        // InvalidCastException with no context.

        var container = new SvcContainer(autoConfigureFromGenerator: false);
        container.Build();
        await using var scope = container.CreateScope();

        // Register a malicious switch that returns the WRONG type — simulates
        // a stale generated dispatch from a mismatched assembly version.
        object capturedResult = null!;
        GeneratedDispatch.RegisterSwitch(
            (requestType, s, request, ct) =>
            {
                capturedResult = ValueTask.FromResult<PicoDI.Abs.VoidResult>(default);
                return capturedResult;
            }
        );

        // Should throw InvalidOperationException with a clear message.
        Exception? caught = null;
        try
        {
            await GeneratedDispatch.Send<CreateOrder, OrderResult>(
                scope,
                new CreateOrder("x", 1),
                CancellationToken.None
            );
        }
        catch (Exception ex)
        {
            caught = ex;
        }

        await Assert.That(caught).IsNotNull();
        await Assert.That(caught!.GetType().Name).Contains("InvalidOperation");
        await Assert.That(caught.Message).Contains("mismatch");
    }

    [Test]
    public async Task Send_WithCorrectGeneratedDispatch_ReturnsTypedResponse()
    {
        // Verify the type-safe dispatch still works for correct registrations.
        var container = new SvcContainer(autoConfigureFromGenerator: false);
        container.RegisterTransient<IRequestHandler<CreateOrder, OrderResult>>(
            _ => new CreateOrderHandler()
        );
        container.Build();
        await using var scope = container.CreateScope();

        // Register a correct switch that returns the matching type.
        GeneratedDispatch.RegisterSwitch(
            (requestType, s, request, ct) =>
            {
                if (requestType == typeof(CreateOrder))
                {
                    var handler = s.GetService<IRequestHandler<CreateOrder, OrderResult>>();
                    return handler!.Handle((CreateOrder)request, ct);
                }
                return null;
            }
        );

        var mediator = new Mediator(scope);
        var result = await mediator.Send<CreateOrder, OrderResult>(new CreateOrder("book", 1));

        await Assert.That(result).IsNotNull();
    }

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

        await Assert.ThrowsAsync(async () =>
            await mediator.Send<CreateOrder, OrderResult>(new CreateOrder("x", 1))
        );
    }
}
