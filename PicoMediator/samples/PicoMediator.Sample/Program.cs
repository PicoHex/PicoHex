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

// ════════════════════════════════════════════════════════════════
// 1. Send: request → response
// ════════════════════════════════════════════════════════════════
Console.WriteLine("1. Send<CreateOrder, OrderResult>");
var result = await mediator.Send<CreateOrder, OrderResult>(new CreateOrder("book", 3));
Console.WriteLine($"   Order created: {result.Id}\n");

// ════════════════════════════════════════════════════════════════
// 2. Send: void command
// ════════════════════════════════════════════════════════════════
Console.WriteLine("2. Send<CancelOrder, VoidResult>");
await mediator.Send<CancelOrder, VoidResult>(new CancelOrder(result.Id));
Console.WriteLine("   Order cancelled.\n");

// ════════════════════════════════════════════════════════════════
// 3. Publish: fan-out notification (sequential)
// ════════════════════════════════════════════════════════════════
Console.WriteLine("3. Publish<OrderCreated> (sequential fan-out)");
await mediator.Publish(new OrderCreated(result.Id, "book"));
Console.WriteLine("   Notification delivered to all handlers.\n");

// ════════════════════════════════════════════════════════════════
// 4. PublishParallel: concurrent fan-out
// ════════════════════════════════════════════════════════════════
Console.WriteLine("4. PublishParallel<OrderCreated> (concurrent fan-out)");

// Register additional handlers to demonstrate parallelism
var container5 = new SvcContainer();
container5.RegisterSingle<INotificationHandler<OrderShipped>>(new SlowShipHandler("Warehouse-A"));
container5.RegisterSingle<INotificationHandler<OrderShipped>>(new SlowShipHandler("Warehouse-B"));
container5.RegisterSingle<INotificationHandler<OrderShipped>>(new SlowShipHandler("Warehouse-C"));
container5.AddPicoMediator();
container5.Build();
await using var scope5 = container5.CreateScope();
var mediator5 = scope5.GetService<IMediator>();

var sw = Stopwatch.StartNew();

// All handlers execute concurrently — total time ≈ slowest handler
await mediator5.PublishParallel(new OrderShipped(result.Id));
Console.WriteLine($"   All handlers completed in {sw.ElapsedMilliseconds}ms\n");

// ════════════════════════════════════════════════════════════════
// 5. OnNoSubscribers callback
// ════════════════════════════════════════════════════════════════
Console.WriteLine("5. OnNoSubscribers callback");

var container6 = new SvcContainer();
container6.AddPicoMediator();
container6.Build();
await using var scope6 = container6.CreateScope();

// Direct Mediator construction for callback demo
var mediator6 = new Mediator(scope6);
mediator6.OnNoSubscribers = typeName =>
    Console.WriteLine($"   [WARN] No handlers registered for '{typeName}'");

await mediator6.Publish(new NoSubscriberNotification());
Console.WriteLine("   Callback invoked, no exception thrown.\n");

// ════════════════════════════════════════════════════════════════
// 6. CancellationToken propagation
// ════════════════════════════════════════════════════════════════
Console.WriteLine("6. CancellationToken");

using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));

try
{
    // Pass a short-lived token — the handler observes it via ct parameter
    await mediator.Send<CreateOrder, OrderResult>(new CreateOrder("urgent", 1), cts.Token);
    Console.WriteLine("   Request completed before timeout.\n");
}
catch (OperationCanceledException)
{
    Console.WriteLine("   Request cancelled by token (as expected).\n");
}

// ════════════════════════════════════════════════════════════════
// 7. Publish: no subscribers (silent drop — default behavior)
// ════════════════════════════════════════════════════════════════
Console.WriteLine("7. Publish<NoSubscriberNotification> (no handlers)");
await mediator.Publish(new NoSubscriberNotification());
Console.WriteLine("   Silently dropped (PUB/SUB semantics).\n");

Console.WriteLine("=== Demo Complete ===");

// ════════════════════════════════════════════════════════════════
// Request types
// ════════════════════════════════════════════════════════════════
public record CreateOrder(string Item, int Qty) : IRequest<OrderResult>;

public record OrderResult(Guid Id);

public record CancelOrder(Guid Id) : IRequest<VoidResult>;

// ════════════════════════════════════════════════════════════════
// Notification types
// ════════════════════════════════════════════════════════════════
public record OrderCreated(Guid OrderId, string Item) : INotification;

public record OrderShipped(Guid OrderId) : INotification;

public record NoSubscriberNotification : INotification;

// ════════════════════════════════════════════════════════════════
// Request handlers
// ════════════════════════════════════════════════════════════════
public sealed class CreateOrderHandler : IRequestHandler<CreateOrder, OrderResult>
{
    public async ValueTask<OrderResult> Handle(CreateOrder r, CancellationToken ct)
    {
        Console.WriteLine($"     [Handler] Creating order: {r.Item} x{r.Qty}");
        // Simulate work that respects cancellation
        await Task.Delay(200, ct);
        ct.ThrowIfCancellationRequested();
        return new OrderResult(Guid.NewGuid());
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

// ════════════════════════════════════════════════════════════════
// Notification handlers
// ════════════════════════════════════════════════════════════════
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

// ── Handler with simulated delay for PublishParallel demo ──
public sealed class SlowShipHandler(string name) : INotificationHandler<OrderShipped>
{
    public async ValueTask Handle(OrderShipped n, CancellationToken ct)
    {
        Console.WriteLine($"     [{name}] Processing shipment for order {n.OrderId}...");
        await Task.Delay(100, ct);
        Console.WriteLine($"     [{name}] Shipment dispatched.");
    }
}
