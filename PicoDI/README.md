# PicoDI

Zero-reflection, AOT-compatible dependency injection for .NET.

## Quick Start

```shell
dotnet add package PicoDI
```

```csharp
using PicoDI;
using PicoDI.Abs;

var container = new SvcContainer();
container.RegisterSingleton<IService>(scope => new MyService());
container.Build();

using var scope = container.CreateScope();
var svc = scope.GetService<IService>();
```

## Registration

All `Register*` methods return `ISvcContainer` for fluent chaining. There are two paths:

### Factory-Based (always works, no source gen required)

```csharp
// Lifetime-specific factory overloads
container.RegisterSingleton<IService>(scope => new Service(scope.GetService<IDep>()));
container.RegisterScoped<IRepository>(scope => new Repository());
container.RegisterTransient<IValidator>(scope => new Validator());

// Generic Register with explicit lifetime
container.Register<ICache>(scope => new MemoryCache(), SvcLifetime.Singleton);
container.Register<ILogger, MyLogger>(scope => new MyLogger(), SvcLifetime.Scoped);

// Pre-built singleton instance
container.RegisterSingle<IClock>(SystemClock.Instance);

// Multiple registrations at once
container.RegisterRange(new[] {
    SvcDescriptor.Create<IServiceA>(scope => new ServiceA()),
    SvcDescriptor.FromInstance(typeof(IServiceB), existingInstance)
});
```

### Type-Based (requires PicoDI.Gen source generator)

```csharp
// These compile to zero-allocation factory delegates
container.RegisterSingleton<IService, Service>();
container.RegisterScoped<IRepository, SqlRepository>();
container.RegisterTransient<IValidator, EmailValidator>();
```

### Open Generics

```csharp
// Open generic registration — closed versions resolved at compile time
container.Register(typeof(IRepository<>), typeof(SqlRepository<>), SvcLifetime.Scoped);
container.Register(typeof(ILogger<>), typeof(ConsoleLogger<>), SvcLifetime.Singleton);
```

### Hosted Services

```csharp
container.RegisterHostedSvc<BackgroundWorker>();
container.RegisterHostedSvc<HealthCheckService>(scope => new HealthCheckService(/* deps */));
```

## Resolution

```csharp
using var scope = container.CreateScope();

// Typed resolution — O(1) via generated Resolve.* methods (with source gen)
// or dictionary lookup (factory-based)
var svc = scope.GetService<IService>();
var repos = scope.GetServices<IRepository>();

// Type-based resolution
var instance = scope.GetService(typeof(IService));
var allInstances = scope.GetServices(typeof(IRepository));

// Try-pattern — no exception for missing services
if (scope.TryGetService(typeof(IOptional), out var optional))
    Console.WriteLine($"Got: {optional}");

if (scope.TryGetServices(typeof(IPlugin), out var plugins))
    foreach (var p in plugins) Console.WriteLine(p);

// IsRegistered — check before resolving
if (container.IsRegistered(typeof(IService)))
    Console.WriteLine("IService is available");
```

## Lifetimes

| Lifetime | Instantiation | Disposal |
|---|---|---|
| **Transient** | New every resolution | Tracked by resolving scope, disposed in reverse creation order (LIFO) |
| **Scoped** | Once per scope | Disposed when scope disposes |
| **Singleton** | Once per container | Disposed when container disposes |

Multiple registrations for the same service type are supported. Resolution returns the last (most recent) registration. `GetServices<T>()` returns all registrations in order.

## Source Generator (PicoDI.Gen)

Add `PicoDI.Gen` as an analyzer to enable compile-time type-based registrations:

```xml
<PackageReference Include="PicoDI.Gen" PrivateAssets="all" />
```

The generator scans all `Register*` call sites in your codebase and emits:

- **`ConfigureGeneratedServices()`** — factory delegates with inlined `new` expressions
- **`Resolve.*` typed resolvers** — zero-dictionary-lookup resolution for registered types
- **Compile-time circular dependency detection** — PICO002 error at build time
- **Open generic metadata** — cross-assembly discovery for `Register(typeof(I<>), typeof(C<>))`
- **`[ModuleInitializer]` auto-configurator** — zero-config startup, no manual Build step needed

The source generator validates implementation types at compile time and emits diagnostics:

| Code | Severity | Description |
|------|----------|-------------|
| PICO002 | Error | Circular dependency detected |
| PICO003 | Error | Abstract type registered as implementation |
| PICO004 | Error | Implementation type has no public constructor |
| PICO005 | Error | Multiple constructors marked with `[SvcConstructor]` |
| PICO006 | Warning | Use generic overload instead of `Register(Type, Type, lifetime)` |

Mark the preferred constructor with `[SvcConstructor]` when the implementation has multiple constructors:

```csharp
public sealed class MyService : IService
{
    [SvcConstructor] // Source generator picks this one
    public MyService(IDepA a, IDepB b) { }

    public MyService(IDepA a) { } // Ignored
}
```

## Hosted Services

### Basic: `IHostedSvc`

```csharp
public sealed class Worker(ILogger<Worker> logger) : IHostedSvc
{
    public async Task StartAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            logger.Info("Working...");
            await Task.Delay(1000, ct);
        }
    }

    public Task StopAsync(CancellationToken ct) => Task.CompletedTask;
}
```

### Advanced: `IHostedLifecycleSvc`

For fine-grained lifecycle control with four-phase startup and shutdown:

```csharp
public sealed class Server(ILogger<Server> logger) : IHostedLifecycleSvc
{
    public async Task StartingAsync(CancellationToken ct) { /* pre-start init */ }
    public async Task StartAsync(CancellationToken ct)       { /* bind port */ }
    public async Task StartedAsync(CancellationToken ct)     { /* post-start */ }
    public async Task StoppingAsync(CancellationToken ct)    { /* drain connections */ }
    public async Task StopAsync(CancellationToken ct)        { /* close port */ }
    public async Task StoppedAsync(CancellationToken ct)     { /* cleanup */ }
}
```

### Convenience: `BackgroundSvc`

Template method pattern — override `ExecuteAsync` only:

```csharp
public sealed class PollingService : BackgroundSvc
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await PollAsync();
            await Task.Delay(5000, stoppingToken);
        }
    }
}
```

### SvcHostBuilder

Fluent builder for hosted services with built-in lifecycle management:

```csharp
var builder = new SvcHostBuilder();
var host = await builder
    .ConfigureServices(container =>
    {
        container.RegisterSingleton<IService>(_ => new Service());
        container.RegisterHostedSvc<Worker>();
    })
    .BuildAsync();

// ... application runs ...

await host.StopAsync();
```

Startup order follows registration order. Shutdown reverses it (LIFO). Each hosted service is a one-shot — once stopped, it cannot be restarted without creating a new container.

## Error Handling

### Resolution Errors

- **Unregistered service**: `GetService()` throws `PicoDiException`. Use `TryGetService()` / `TryGetServices()` for optional dependencies.
- **Disposed scope/container**: Throws `ObjectDisposedException`.
- **Source generator not applied**: Throws `SourceGeneratorRequiredException` for type-based registrations.

### Disposal Errors

Disposal is fault-tolerant — one service failing to dispose does not prevent others from being cleaned up. Errors are reported via the `OnError` callback:

```csharp
var container = new SvcContainer();
container.OnError = (ex, context) =>
{
    Console.Error.WriteLine($"[{context}] {ex.Message}");
};
```

## Thread Safety

PicoDI is designed for concurrent use:

- **Registration**: Serialized under lock. Must complete before `Build()`.
- **`Build()`**: Idempotent. Freezes registration cache into lock-free `FrozenDictionary`.
- **Resolution**: Lock-free reads. Singleton creation uses deadlock-safe double-checked locking. Scoped instances use `ConcurrentDictionary` + `Lazy<T>`.
- **Disposal**: Idempotent. `DisposeAsync()` can be called concurrently and multiple times safely.

## Packages

| Package | Description |
|---|---|
| **PicoDI** | DI container runtime |
| **PicoDI.Abs** | Abstractions: `ISvcContainer`, `ISvcScope`, `SvcDescriptor`, `SvcLifetime`, `IHostedSvc`, `BackgroundSvc` |
| **PicoDI.Gen** | Roslyn source generator + diagnostic analyzer |

[← Back to PicoHex](../README.md)
