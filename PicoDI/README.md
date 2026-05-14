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

All `Register*` methods return `ISvcContainer` for fluent chaining.

```csharp
// Factory-based (no source gen required)
container.RegisterSingleton<IService>(scope => new Service(scope.GetService<IDep>()));
container.RegisterScoped<IRepository>(scope => new Repository());
container.RegisterTransient<IValidator>(scope => new Validator());

// Pre-built instance
container.RegisterSingle<IClock>(SystemClock.Instance);

// Open generics
container.Register(typeof(IRepository<>), typeof(SqlRepository<>), SvcLifetime.Scoped);

// Type-based (requires PicoDI.Gen)
container.RegisterSingleton<IService, Service>();

// Hosted services
container.RegisterHostedSvc<BackgroundWorker>();

container.Build();
```

## Resolution

```csharp
using var scope = container.CreateScope();

// Typed resolution (zero-lookup via generated Resolve.* methods)
var svc = scope.GetService<IService>();
var repos = scope.GetServices<IRepository>();

// Type-based fallback
var instance = scope.GetService(typeof(IService));
```

## Lifetimes

| Lifetime | Instantiation | Disposal |
|---|---|---|
| **Transient** | New every time | Tracked by resolving scope, disposed in LIFO order |
| **Scoped** | Once per scope | Disposed when scope disposes |
| **Singleton** | Once per container | Disposed when container disposes |

## Source Generator (PicoDI.Gen)

Add `PicoDI.Gen` as an analyzer to enable compile-time type-based registrations:

```xml
<PackageReference Include="PicoDI.Gen" PrivateAssets="all" />
```

The generator emits:
- `ConfigureGeneratedServices()` with inline factory delegates
- Typed `Resolve.*` methods for zero-lookup resolution
- Compile-time circular dependency detection
- Open generic metadata for cross-assembly discovery
- `[ModuleInitializer]` auto-configurator

## SvcHostBuilder

Optional fluent builder for hosted services:

```csharp
using var hostBuilder = new SvcHostBuilder();
var host = await hostBuilder
    .ConfigureServices(container =>
    {
        container.RegisterSingleton<IService>(_ => new Service());
        container.RegisterHostedSvc<Worker>();
    })
    .BuildAsync();
await host.StopAsync();
```

## Packages

| Package | Description |
|---|---|
| **PicoDI** | DI container |
| **PicoDI.Abs** | `ISvcContainer`, `ISvcScope`, `SvcDescriptor` |
| **PicoDI.Gen** | Roslyn source generator |

[← Back to PicoHex](../README.md)
