Console.WriteLine("=== PicoMediator Demo ===\n");

// ── Setup ──────────────────────────────────────────────────────
var container = new SvcContainer();

// Register handlers
container.RegisterScoped<IRequestHandler<CreateOrder, OrderResult>>(_ => new CreateOrderHandler());
container.RegisterScoped<IRequestHandler<CancelOrder, VoidResult>>(_ => new CancelOrderHandler());
container.RegisterSingle<INotificationHandler<OrderCreated>>(new OrderCreatedEmailHandler());
container.RegisterSingle<INotificationHandler<OrderCreated>>(new OrderCreatedAuditHandler());

container.AddPicoMediator();
container.Build();

await using var scope = container.CreateScope();
var mediator = scope.GetService<IMediator>();

// ── Send: request → response ───────────────────────────────────
Console.WriteLine("1. Send<CreateOrder, OrderResult>");
var result = await mediator.Send<CreateOrder, OrderResult>(new CreateOrder("book", 3));
Console.WriteLine($"   Order created: {result.Id}\n");

// ── Send: void command ─────────────────────────────────────────
Console.WriteLine("2. Send<CancelOrder, VoidResult>");
await mediator.Send<CancelOrder, VoidResult>(new CancelOrder(result.Id));
Console.WriteLine("   Order cancelled.\n");

// ── Publish: fan-out notification ──────────────────────────────
Console.WriteLine("3. Publish<OrderCreated>");
await mediator.Publish(new OrderCreated(result.Id, "book"));
Console.WriteLine("   Notification published to all handlers.\n");

// ── Publish: no subscribers (silent) ───────────────────────────
Console.WriteLine("4. Publish<NoSubscriberNotification> (no handlers)");
await mediator.Publish(new NoSubscriberNotification());
Console.WriteLine("   Silently dropped (PUB/SUB semantics).\n");

Console.WriteLine("=== Demo Complete ===");

// ════════════════════════════════════════════════════════════════
// Request types
public record CreateOrder(string Item, int Qty) : IRequest<OrderResult>;

public record OrderResult(Guid Id);

public record CancelOrder(Guid Id) : IRequest<VoidResult>;

// Handlers
public sealed class CreateOrderHandler : IRequestHandler<CreateOrder, OrderResult>
{
    public ValueTask<OrderResult> Handle(CreateOrder r, CancellationToken ct)
    {
        Console.WriteLine($"     [Handler] Creating order: {r.Item} x{r.Qty}");
        return ValueTask.FromResult(new OrderResult(Guid.NewGuid()));
    }
}

public sealed class CancelOrderHandler : IRequestHandler<CancelOrder, VoidResult>
{
    public ValueTask<VoidResult> Handle(CancelOrder r, CancellationToken ct)
    {
        Console.WriteLine($"     [Handler] Cancelling order {r.Id}");
        return ValueTask.FromResult(default(VoidResult));
    }
}

// Notifications
public record OrderCreated(Guid OrderId, string Item) : INotification;

public record NoSubscriberNotification : INotification;

// Notification handlers
public sealed class OrderCreatedEmailHandler : INotificationHandler<OrderCreated>
{
    public ValueTask Handle(OrderCreated n, CancellationToken ct)
    {
        Console.WriteLine($"     [Email] Sending confirmation for order {n.OrderId}");
        return ValueTask.CompletedTask;
    }
}

public sealed class OrderCreatedAuditHandler : INotificationHandler<OrderCreated>
{
    public ValueTask Handle(OrderCreated n, CancellationToken ct)
    {
        Console.WriteLine($"     [Audit] Logging order {n.OrderId} ({n.Item})");
        return ValueTask.CompletedTask;
    }
}
