# PicoHex

**AOT-First Universal Minimal Infrastructure for .NET**

The minimal universal infrastructure for production-grade .NET applications &mdash; three modules, eleven packages, zero runtime reflection.

[![NuGet](https://img.shields.io/nuget/v/PicoDI)](https://nuget.org/packages/PicoDI)
[![License: MIT](https://img.shields.io/badge/License-MIT-green.svg)](LICENSE)
[![CI](https://github.com/PicoHex/PicoHex/actions/workflows/ci.yml/badge.svg)](https://github.com/PicoHex/PicoHex/actions/workflows/ci.yml)

[English](README.md) | [简体中文](README.zh.md) | [日本語](README.ja.md) | [Español](README.es.md) | [Português](README.pt.md) | [繁體中文](README.zh-tw.md) | [한국어](README.ko.md) | [Français](README.fr.md) | [Deutsch](README.de.md) | [Русский](README.ru.md)

---

## Computational Model

```
Configuration  ──→  Dependency Injection  ──→  Logging
  (Input)              (Core)                   (Output)
```

Every application reads configuration, assembles its internals, and produces output. **PicoHex** is the minimal implementation of these three meta-operations for .NET Native AOT &mdash; designed from the ground up for compile-time code generation instead of runtime reflection.

---

## Why PicoHex

| | Microsoft.Extensions | PicoHex |
|---|---|---|
| **Packages** | 40+ | 11 |
| **Runtime reflection** | Heavy (`Activator.CreateInstance`, Expression trees) | Zero (source generators) |
| **AOT ready** | Requires opt-in and careful config | AOT First &mdash; compiles natively out of the box |
| **HostBuilder required** | Yes &mdash; required to wire DI + Config + Logging | No &mdash; `new SvcContainer()` is all you need |
| **Module integration** | Runtime registration (`IServiceCollection`) | Compile-time via `ModuleInitializer` + source generators |
| **Cold start** | Slow (JIT + reflection) | Fast (pre-generated code paths) |
| **Binary size** | Large (pulls in many assemblies) | Minimal (trimmable, linker-friendly) |

---

## Performance

Benchmarks run on **.NET 10.0.5, Windows 10, X64, Native AOT, Release mode**.

### PicoDI vs Microsoft.Extensions.DependencyInjection

**20/20 wins, average 2.83&times; faster, max 4.00&times; (DeepChain &times; Transient).**

| Scenario | PicoDI (ns) | MsDI (ns) | Speedup |
|---|---|---|---|
| **DeepChain &times; Transient** | 156.7 | 626.9 | **4.00&times;** |
| **NoDependency &times; Singleton** | 14.2 | 55.1 | **3.89&times;** |
| **MultipleResolutions &times; Singleton** | 1,523.6 | 5,373.9 | **3.53&times;** |
| **MultipleResolutions &times; Transient** | 4,672.4 | 16,402.1 | **3.51&times;** |
| **NoDependency &times; Transient** | 27.7 | 97.1 | **3.50&times;** |
| ContainerSetup | 739.9 | 1,919.1 | 2.59&times; |
| SingleResolution &times; Transient | 55.3 | 187.8 | 3.39&times; |
| ScopeCreation | 94.1 | 104.5 | 1.11&times; |

**AOT binary: 3,087.5 KB**

### PicoCfg vs Microsoft.Extensions.Configuration

**1.35&times;&ndash;1.57&times; faster on mixed workloads.**

| Scenario | PicoCfg (ns) | MsConfig (ns) | Speedup |
|---|---|---|---|
| Mixed n=100, p=2, l=1 | 5,920.9 | 9,273.9 | **1.57&times;** |
| Mixed n=100, p=2, l=10 | 29,831.8 | 40,218.9 | **1.35&times;** |

**AOT binary: 2,476.5 KB**

### PicoLog vs Microsoft.Extensions.Logging

PicoLog constructs richer log entries (timestamp, category, scopes, properties), which is why the async handoff path is comparable to Microsoft's lightweight string-channel handoff. The **control benchmarks** measure the underlying efficiency without queue/sink overhead:

| Control Benchmark | N=1 | N=10 | N=100 |
|---|---|---|---|
| **TimestampNowOnly** | 4.15&times; | 5.05&times; | 4.54&times; |
| **LogEntryAllocateOnly** | 8.48&times; | 23.48&times; | 20.11&times; |

**AOT binary: 3,020 KB**

---

## Quick Start

### Just Configuration

```shell
dotnet add package PicoCfg
```

```csharp
using PicoCfg;
using PicoCfg.Abs;
using PicoCfg.Extensions;

var cfg = await Cfg.CreateBuilder()
    .Add("App:Name=MyApp\nApp:Version=1.0")
    .BuildAsync();

var value = cfg.GetValue("App:Name");
```

### Just DI

```shell
dotnet add package PicoDI
```

```csharp
using PicoDI;
using PicoDI.Abs;

var container = new SvcContainer();
container.RegisterSingleton<IService>(scope => new MyService());
container.Build();

var svc = container.CreateScope().GetService<IService>();
```

### Just Logging

```shell
dotnet add package PicoLog
```

```csharp
using PicoLog;
using PicoLog.Abs;

var sink = new ColoredConsoleSink(new ConsoleFormatter());
using var factory = new LoggerFactory([sink],
    new LoggerFactoryOptions { MinLevel = LogLevel.Info });
var logger = factory.CreateLogger("App");
logger.Info("Application started");
```

---

## PicoDI

Zero-reflection DI container with compile-time source generation.

### Registration

All `Register*` methods return `ISvcContainer` for fluent chaining.

```csharp
var container = new SvcContainer();

// Factory-based (always works, no source gen required)
container.RegisterSingleton<IService>(scope => new Service(scope.GetService<IDep>()));
container.RegisterScoped<IRepository>(scope => new Repository());
container.RegisterTransient<IValidator>(scope => new Validator());

// Pre-built instance
container.RegisterSingle<IClock>(SystemClock.Instance);

// Open generics
container.Register(typeof(IRepository<>), typeof(SqlRepository<>), SvcLifetime.Scoped);

// Type-based (requires PicoDI.Gen source generator)
container.RegisterSingleton<IService, Service>();

// Hosted services
container.RegisterHostedSvc<BackgroundWorker>();

container.Build();  // Freeze registrations
```

### Resolution

```csharp
using var scope = container.CreateScope();

// Typed resolution (zero-lookup via generated Resolve.* methods)
var svc = scope.GetService<IService>();
var repos = scope.GetServices<IRepository>();

// Type-based resolution
var instance = scope.GetService(typeof(IService));
```

### Lifetimes

| Lifetime | Instantiation | Disposal |
|---|---|---|
| **Transient** | New every time | Tracked by resolving scope, disposed in LIFO order |
| **Scoped** | Once per scope | Disposed when scope disposes |
| **Singleton** | Once per container | Disposed when container disposes |

### Source Generator (PicoDI.Gen)

Add `PicoDI.Gen` as an analyzer to enable compile-time type-based registrations:

```xml
<PackageReference Include="PicoDI.Gen" PrivateAssets="all" />
```

The generator scans all `Register*` calls and emits:

- **`ConfigureGeneratedServices()`** extension method with inline factory delegates (no reflection)
- **Typed `Resolve.*`** methods for zero-lookup resolution paths
- **Compile-time circular dependency detection**
- **Open generic metadata** for cross-assembly discovery
- **`[ModuleInitializer]`** auto-configurator that registers with `SvcContainerAutoConfiguration`

**Before** (your code):
```csharp
var container = new SvcContainer();
container.RegisterSingleton<IService, Service>();
container.Build();
```

**After** (generated):
```csharp
// Auto-generated by PicoDI.Gen
partial class SvcContainerGeneratedConfiguration
{
    [ModuleInitializer]
    public static void Initialize() =>
        SvcContainerAutoConfiguration.RegisterConfigurator(
            "PicoDI.Generated",
            c => c.RegisterSingleton(typeof(IService),
                _ => new Service(_.GetService<IDep>())));
}
```

### SvcHostBuilder

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

// Application runs until stopped
await host.StopAsync();
```

---

## PicoCfg

Async-first configuration management with provider model.

### Provider Model

```
Sources ──→ Providers ──→ Root ──→ Consumer
```

Sources define **how** config is produced. Providers are the materialized instances. The root composes provider snapshots into a unified view. Consumers query via `TryGetValue` or `GetValue`.

### Built-in Sources

| Source | Description |
|---|---|
| **Dictionary** | In-memory key-value pairs |
| **Environment Variables** | OS environment, prefix filtering, `__` &rarr; `:` mapping |
| **Command Line** | `--key=value`, `--key value`, `-key value`, `/key value` |
| **Stream** | Line-based `key=value` text parsing with file watching |
| **File Watching** | Auto-reload on file change with debounce |
| **Chained** | Fallback to another `ICfg` instance |
| **KeyPerFile** | Kubernetes ConfigMap style &mdash; filename=key, content=value |

### CfgBuilder

```csharp
using PicoCfg;
using PicoCfg.Abs;
using PicoCfg.Extensions;

var cfg = await Cfg.CreateBuilder()
    .AddEnvironmentVariables("APP_")
    .Add(() => File.OpenRead("appsettings.cfg"))
    .Add(() => File.OpenRead($"appsettings.{env}.cfg"),
        watchPath: $"appsettings.{env}.cfg")
    .AddCommandLine(args)
    .BuildAsync();

var name = cfg.GetValue("App:Name");
```

### Source-Generated Binding

Add `PicoCfg.Gen` as an analyzer. The source generator discovers binding targets at compile time from `CfgBind.Bind<T>` / `CfgBind.TryBind<T>` / `CfgBind.BindInto<T>` call sites — no attributes required. Generated code registers strongly-typed binders that resolve configuration values by key path without runtime reflection.

```csharp
using PicoCfg;

public sealed class AppSettings
{
    public string Name { get; init; }
    public int MaxRetries { get; init; } = 3;
    public bool EnableFeature { get; init; }
}

// Call site triggers source generation for AppSettings
var settings = CfgBind.Bind<AppSettings>(cfg, "App");
```

---

## PicoLog

Structured logging with compile-time message templates.

### Log Levels

| Level | Value | Usage |
|---|---|---|
| **Emergency** | 0 | System is unusable |
| **Alert** | 1 | Action must be taken immediately |
| **Critical** | 2 | Critical conditions |
| **Error** | 3 | Error conditions |
| **Warning** | 4 | Warning conditions |
| **Notice** | 5 | Normal but significant |
| **Info** | 6 | Informational messages |
| **Debug** | 7 | Debug-level messages |
| **Trace** | 8 | Detailed diagnostic tracing |
| **None** | 255 | Disables all logging |

### Message Templates

Support for both string interpolation and `FormattableString` with named parameters for structured logging:

```csharp
logger.Info($"Processing order {orderId} for {customer}");
logger.Log(LogLevel.Info, $"User {user} logged in from {ipAddress}");

// Structured logging with explicit properties
logger.LogStructured(LogLevel.Error, "Payment failed",
    properties: [new("OrderId", orderId), new("Amount", amount)],
    exception: ex);
```

### Source-Generated Messages

`[PicoLogMessage]` attribute on static partial methods emits strongly-typed, AOT-compatible extension methods:

```csharp
public static partial class AppLogs
{
    [PicoLogMessage(LogLevel.Info, EventId = 1001, Message = "Order {OrderId} placed")]
    public static partial void OrderPlaced(this ILogger logger, string orderId);

    [PicoLogMessage(LogLevel.Error, EventId = 2001, Message = "Payment failed")]
    public static partial void PaymentFailed(this ILogger logger, string orderId, Exception ex);
}

// Usage
logger.OrderPlaced("ORD-12345");
logger.PaymentFailed("ORD-12345", ex);
```

### Built-in Sinks

```csharp
var sinks = new List<ILogSink>
{
    new ConsoleSink(new ConsoleFormatter()),          // Plain console
    new ColoredConsoleSink(new ConsoleFormatter()),    // Color-coded by level
    new FileSink(new ConsoleFormatter(),               // Batched file output
        new FileSinkOptions { FilePath = "app.log", BatchSize = 100 })
};
using var factory = new LoggerFactory(sinks);
```

### Custom Sinks

```csharp
public sealed class CustomSink : ILogSink
{
    public Task WriteAsync(LogEntry entry, CancellationToken ct = default)
    {
        // Write to your backend
        return Task.CompletedTask;
    }

    public void Dispose() { }
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
```

---

## Module Integration

When `PicoDI`, `PicoCfg.DI`, and `PicoLog.DI` are all referenced, wiring happens automatically:

1. Each module's source generator registers a `ModuleInitializer`-based configurator
2. `new SvcContainer()` runs all configurators via `SvcContainerAutoConfiguration.TryApplyConfiguration`
3. No manual wiring needed

### DI Integration APIs

**PicoCfg.DI** &mdash; extensions on `ISvcContainer`:
- `RegisterCfgRoot(ICfgRoot root)` &mdash; registers `ICfgRoot` and `ICfg`
- `RegisterCfgTransient/Scoped/Singleton<T>(string? section)` &mdash; bind POCOs from config
- `RegisterCfgOptionsSingleton/Scoped<T>(string? section)` &mdash; typed options support

**PicoLog.DI** &mdash; extension on `ISvcContainer`:
- `AddPicoLog(Action<LoggingOptions> configure)` &mdash; registers `ILoggerFactory` and `ILogger<>`
- `WriteTo.Console()` / `.ColoredConsole()` / `.File(path)` / `.Sink(custom)` &mdash; sink configuration
- `ReadFrom.RegisteredSinks()` &mdash; include DI-registered `ILogSink` instances

### Minimal Combined Example

```shell
dotnet add package PicoDI
dotnet add package PicoCfg.DI
dotnet add package PicoLog.DI
```

```csharp
using PicoDI;
using PicoDI.Abs;
using PicoCfg;
using PicoCfg.Abs;
using PicoCfg.DI;
using PicoCfg.Extensions;
using PicoLog.Abs;
using PicoLog.DI;

var container = new SvcContainer();
var cfg = await Cfg.CreateBuilder()
    .AddEnvironmentVariables("APP_")
    .Add(() => File.OpenRead("appsettings.cfg"))
    .BuildAsync();
container.RegisterCfgRoot(cfg);

container.AddPicoLog(o =>
{
    o.MinLevel = LogLevel.Info;
    o.WriteTo.ColoredConsole();
});
var logger = container.CreateScope().GetService<ILogger<Program>>();
logger.Info("App started");
```

---

## Design Philosophy

**克制 (Restraint)** — Only DI, Config, Logging. No Web framework, no ORM, no message queue. If it's not universal infrastructure, it doesn't belong. PicoHex is the common denominator that every app needs — nothing more, nothing less.

**专注 (Focus)** — Each module does one thing. PicoDI is a container, not a service locator. PicoCfg is configuration, not a feature flag system. PicoLog is logging, not a telemetry pipeline. Deep specialization over shallow generality.

**优雅 (Elegance)** — APIs are minimal. Source generators do the wiring at compile time. The developer writes straightforward code; the tooling handles the complexity. `new SvcContainer()` replaces 100+ lines of `Host.CreateDefaultBuilder()` ceremony.

**高效 (Efficiency)** — AOT First is not an afterthought — it's the foundation. Zero reflection. Zero runtime overhead. Everything that can be resolved at compile time is resolved at compile time. Minimal binaries, fast cold starts, predictable performance.

---

## Use Cases

| Scenario | PicoDI | PicoCfg | PicoLog | Why |
|---|---|---|---|---|
| **CLI Tools** | Optional | Essential | Essential | Parse args/config, control output verbosity. DI is there if the tool grows. |
| **Serverless / Lambda** | Essential | Essential | Essential | Cold start is the bottleneck. AOT-compiled DI with pre-generated resolution paths. |
| **WASM / Blazor** | Essential | Essential | Essential | Download size matters. 11 packages, trim-friendly, no runtime bloat. |
| **Embedded / IoT** | When needed | Essential | Essential | Resource-constrained devices. Minimal binary, zero allocations where possible. |

---

## Packages

| Package | Description | NuGet |
|---|---|---|
| **PicoDI** | Zero-reflection DI container with compile-time source generation | [![NuGet](https://img.shields.io/nuget/v/PicoDI)](https://nuget.org/packages/PicoDI) |
| **PicoDI.Abs** | Abstractions for PicoDI (`ISvcContainer`, `ISvcScope`, `SvcDescriptor`) | [![NuGet](https://img.shields.io/nuget/v/PicoDI.Abs)](https://nuget.org/packages/PicoDI.Abs) |
| **PicoDI.Gen** | Roslyn source generator &mdash; compile-time registration and resolution | [![NuGet](https://img.shields.io/nuget/v/PicoDI.Gen)](https://nuget.org/packages/PicoDI.Gen) |
| **PicoCfg** | Async-first configuration management with provider model | [![NuGet](https://img.shields.io/nuget/v/PicoCfg)](https://nuget.org/packages/PicoCfg) |
| **PicoCfg.Abs** | Abstractions for PicoCfg (`ICfg`, `ICfgRoot`, `ICfgSection`) | [![NuGet](https://img.shields.io/nuget/v/PicoCfg.Abs)](https://nuget.org/packages/PicoCfg.Abs) |
| **PicoCfg.Gen** | Source generator for typed config binding (`CfgBind.Bind<T>`) | [![NuGet](https://img.shields.io/nuget/v/PicoCfg.Gen)](https://nuget.org/packages/PicoCfg.Gen) |
| **PicoCfg.DI** | PicoDI integration for PicoCfg (`RegisterCfgRoot`, `RegisterCfgSingleton`) | [![NuGet](https://img.shields.io/nuget/v/PicoCfg.DI)](https://nuget.org/packages/PicoCfg.DI) |
| **PicoLog** | Structured logging with compile-time message templates | [![NuGet](https://img.shields.io/nuget/v/PicoLog)](https://nuget.org/packages/PicoLog) |
| **PicoLog.Abs** | Abstractions for PicoLog (`ILogger`, `ILogSink`, `LogLevel`) | [![NuGet](https://img.shields.io/nuget/v/PicoLog.Abs)](https://nuget.org/packages/PicoLog.Abs) |
| **PicoLog.Gen** | Source generator for `[PicoLogMessage]` methods | [![NuGet](https://img.shields.io/nuget/v/PicoLog.Gen)](https://nuget.org/packages/PicoLog.Gen) |
| **PicoLog.DI** | PicoDI integration for PicoLog (`AddPicoLog`, `WriteTo`, `ReadFrom`) | [![NuGet](https://img.shields.io/nuget/v/PicoLog.DI)](https://nuget.org/packages/PicoLog.DI) |

---

## Comparison

### vs Autofac

Autofac is a mature, feature-rich DI container with property injection, decorators, interceptors, and modules. PicoDI takes the opposite approach: **zero runtime reflection, compile-time-only registration, AOT-safe by design**. If you need runtime flexibility, use Autofac. If you need **AOT and minimal overhead**, use PicoDI.

### vs Lamar

Lamar is a performant DI container that uses runtime code generation (`DynamicAssembly` + IL emit). PicoDI uses Roslyn source generators for **compile-time** code generation. Lamar supports more features (interception, decoration); PicoDI is minimal and AOT-first.

### vs Serilog

Serilog is the gold standard for structured logging in .NET with a vast sink ecosystem. PicoLog is not a Serilog replacement &mdash; it's a **lightweight alternative** for projects that value AOT compatibility and minimal dependencies over sink variety. PicoLog's source generator for message templates gives comparable structured logging quality.

### vs Microsoft.Extensions

PicoHex is not an extension to `Microsoft.Extensions` &mdash; it's an **alternative**. Designed from the ground up for Native AOT, with compile-time code generation instead of runtime reflection. If you're building traditional ASP.NET applications, stick with Microsoft.Extensions. If you're building **CLI tools, serverless functions, WASM apps, or embedded systems**, PicoHex is built for your use case.

---

## License &amp; Contributing

MIT License. Repository: [https://github.com/PicoHex/PicoHex](https://github.com/PicoHex/PicoHex)
