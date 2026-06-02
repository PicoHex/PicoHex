# PicoMediator

Compile-time request/notification dispatch for PicoDI. Zero reflection, AOT-first.

## Quick Start

```shell
dotnet add package PicoMediator
```

```csharp
using PicoDI;
using PicoMediator;
using PicoMediator.Abs;

var container = new SvcContainer();

// Register handlers
container.Register<IRequestHandler<Ping, string>, PingHandler>(SvcLifetime.Transient);
container.RegisterSingle<INotificationHandler<OrderCreated>>(new OrderCreatedEmailHandler());

// Register mediator
container.AddPicoMediator();
container.Build();

await using var scope = container.CreateScope();
var mediator = scope.GetService<IMediator>();

// Send: 1:1 request → response
var result = await mediator.Send<Ping, string>(new Ping());

// Publish: 1:N notification → all subscribers
await mediator.Publish(new OrderCreated(Guid.NewGuid(), "book"));
```

## Core Concepts

### Interaction Patterns

PicoMediator follows the ZeroMQ-inspired principle: **interaction patterns are atomic primitives encoded by the type system**.

| Pattern | ZeroMQ | C# Type | Cardinality | Response |
|---------|--------|---------|:---:|:---:|
| Request | REQ/REP | `IRequest<TResponse>` | 1:1 | Yes |
| Notification | PUB/SUB | `INotification` | 1:N | No |

No `ICommand`/`IQuery` split — define your own via interface inheritance if needed:

```csharp
public interface ICommand<T> : IRequest<T> { }
public interface IQuery<T> : IRequest<T> { }
```

No `IStreamRequest` — wrap `IAsyncEnumerable<T>` in the response:

```csharp
public record ExportUsers : IRequest<ExportUsersResponse>;
public record ExportUsersResponse(IAsyncEnumerable<User> Users);
```

### Void Commands

Use `VoidResult` (from `PicoDI.Abs`) for fire-and-forget commands:

```csharp
public record DeleteOrder(Guid Id) : IRequest<VoidResult>;

public class DeleteOrderHandler : IRequestHandler<DeleteOrder, VoidResult>
{
    public async ValueTask<VoidResult> Handle(DeleteOrder r, CancellationToken ct)
    {
        await DeleteAsync(r.Id, ct);
        return default;
    }
}
```

### Publisher/Subscriber Semantics

Publish follows ZeroMQ PUB/SUB semantics:
- Publisher does not know subscribers
- No return value (protocol forbids it)
- No subscribers → **silent drop** (not an error)
- Multiple subscribers → each receives the notification

```csharp
// 2 subscribers
container.RegisterSingle<INotificationHandler<OrderCreated>>(new EmailHandler());
container.RegisterSingle<INotificationHandler<OrderCreated>>(new AuditHandler());

await mediator.Publish(new OrderCreated(id, item));
// → EmailHandler.Handle() called
// → AuditHandler.Handle() called
```

## Registration

### Handlers

Handlers are standard PicoDI registrations:

```csharp
// Factory-based
container.RegisterTransient<IRequestHandler<Ping, string>>(_ => new PingHandler());
container.RegisterScoped<IRequestHandler<CreateOrder, OrderResult>>(_ => new CreateOrderHandler());

// Instance
container.RegisterSingle<INotificationHandler<OrderCreated>>(new EmailHandler());

// Type-based (requires PicoDI.Gen source generator)
container.Register<IRequestHandler<Ping, string>, PingHandler>(SvcLifetime.Transient);
```

### Mediator

One line — registers `IMediator` as Scoped:

```csharp
container.AddPicoMediator();
```

### Narrow Ports

Depend on the narrowest interface for your component:

```csharp
// Only sends requests
public sealed class OrderController(ISender sender) { ... }

// Only publishes notifications
public sealed class EventSource(IPublisher publisher) { ... }

// Orchestration — needs both
public sealed class CheckoutService(IMediator mediator) { ... }
```

## Pipeline Behaviors (via PicoAop)

PicoMediator does NOT define its own pipeline abstraction. Use PicoAop interceptors instead:

```csharp
// Mediator-level — applies to all Send/Publish calls
container.Register<IMediator, Mediator>(SvcLifetime.Scoped)
    .InterceptBy<MetricsInterceptor>();

// Handler-level — applies to a specific request type
container.Register<IRequestHandler<CreateOrder, OrderResult>, CreateOrderHandler>(SvcLifetime.Transient)
    .InterceptBy<LoggingInterceptor>()
    .InterceptBy<ValidationInterceptor>()
    .InterceptBy<TransactionInterceptor>();

// Decorator chain (onion model):
// Transaction → Validation → Logging → Handler
```

## Source Generator (PicoMediator.Gen)

Add `PicoMediator.Gen` as an analyzer:

```xml
<PackageReference Include="PicoMediator.Gen" PrivateAssets="all" />
```

The generator scans `IRequestHandler<T, T>` implementations and emits:

- **`GeneratedMediatorDispatch.Send()`** — switch-based dispatch with `Unsafe.As` cast (zero allocation)
- **Runtime fallback** — `scope.GetService<T>()` for handlers not in the switch table

Without the generator, `Mediator.Send()` still works via the runtime `GetService` fallback.

## Error Handling

| Scenario | Behavior |
|----------|----------|
| Send — no handler registered | `InvalidOperationException` |
| Send — handler throws | Exception propagates to caller |
| Publish — no subscribers | Silent (PUB/SUB semantics) |
| Publish — one handler throws | Exception propagates to caller |
| Publish — multiple handlers fail | `AggregateException` |

## Packages

| Package | Description |
|---|---|
| **PicoMediator.Abs** | `IRequest<T>`, `INotification`, `IRequestHandler<T, T>`, `INotificationHandler<T>`, `ISender`, `IPublisher`, `IMediator` |
| **PicoMediator** | `Mediator(ISvcScope)` runtime |
| **PicoMediator.Gen** | Source generator — switch dispatch |
| **PicoMediator.DI** | `container.AddPicoMediator()` |

[← Back to PicoHex](../README.md)
