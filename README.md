# PicoHex

**AOT-First Universal Minimal Infrastructure for .NET**

All built with zero runtime reflection.

[![NuGet](https://img.shields.io/nuget/v/PicoDI)](https://nuget.org/packages/PicoDI)
[![License: MIT](https://img.shields.io/badge/License-MIT-green.svg)](https://github.com/PicoHex/PicoHex/blob/main/LICENSE)
[![CI](https://github.com/PicoHex/PicoHex/actions/workflows/ci.yml/badge.svg)](https://github.com/PicoHex/PicoHex/actions/workflows/ci.yml)

[English](README.md) | [简体中文](README.zh.md) | [日本語](README.ja.md) | [Español](README.es.md) | [Português](README.pt.md) | [繁體中文](README.zh-tw.md) | [한국어](README.ko.md) | [Français](README.fr.md) | [Deutsch](README.de.md) | [Русский](README.ru.md)

---

## Computational Model

```
Configuration  ──→  Dependency Injection  ──→  Logging
  (Input)              (Core)                   (Output)
```

Every application reads configuration, assembles its internals, and produces output. **PicoHex** provides the minimal, AOT-first implementation of these operations for .NET — configuration (PicoCfg), dependency injection (PicoDI), logging (PicoLog), compile-time AOP (PicoAop), and mediator dispatch (PicoMediator). All built on source generators instead of runtime reflection.

---

## Why PicoHex

| | Microsoft.Extensions | PicoHex |
|---|---|---|
| **Packages** | Many | Minimal |
| **Runtime reflection** | Heavy (`Activator.CreateInstance`, Expression trees) | Zero (source generators) |
| **AOT ready** | Requires opt-in and careful config | AOT First — compiles natively out of the box |
| **HostBuilder required** | Yes | No — `new SvcContainer()` is all you need |
| **Module integration** | Runtime registration (`IServiceCollection`) | Compile-time via `ModuleInitializer` + source generators |
| **Cold start** | Fast (JIT) | Fast (pre-generated code paths) |
| **Binary size** | Large | Minimal (trimmable, linker-friendly) |

---

## Quick Start

### Just Configuration

```shell
dotnet add package PicoCfg
```

```csharp
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
var container = new SvcContainer();
container.RegisterSingleton<IService>(scope => new MyService());
container.Build();
await using var scope = container.CreateScope();
var svc = scope.GetService<IService>();
```

PicoDI also supports **compile-time AOP/interceptors** — chain `.InterceptBy<TInterceptor>()` after `Register()` and the source generator emits decorator classes at build time. rebuilt with zero-allocation — the next-generation AOT-first interception engine with zero-allocation invocation structs. [Learn more →](PicoAop/README.md)

### Just AOP

```shell
dotnet add package PicoAop.Abs
dotnet add package PicoDI  # PicoDI.Gen bundled, detects InterceptBy
```

```csharp
container.RegisterScoped<IService, MyService>()
    .InterceptBy<LoggingInterceptor>();
// PicoAop.Gen generates zero-allocation Invocation structs + proxy
// PicoDI.Gen rewrites registration to use the wrapped factory
```

### Just Mediator

```shell
dotnet add package PicoMediator
```

```csharp
container.Register<IRequestHandler<Ping, string>, PingHandler>(SvcLifetime.Transient);
container.AddPicoMediator();
container.Build();
var mediator = scope.GetService<IMediator>();
var result = await mediator.Send<Ping, string>(new Ping());
```

Pipeline behaviors = PicoAop interceptors. Mediator does routing, AOP does decoration.

### Just Logging

```shell
dotnet add package PicoLog
```

```csharp
var sink = new ColoredConsoleSink(new ConsoleFormatter());
using var factory = new LoggerFactory([sink],
    new LoggerFactoryOptions { MinLevel = LogLevel.Info });
var logger = factory.CreateLogger("App");
logger.Info("Application started");
```

### All Together

```shell
dotnet add package PicoCfg.DI
dotnet add package PicoLog.DI
```

```csharp
var container = new SvcContainer();
var cfg = await Cfg.CreateBuilder()
    .AddEnvironmentVariables("APP_")
    .AddCommandLine(args)
    .BuildAsync();
container.RegisterCfgRoot(cfg);
container.AddPicoLog(o => { o.MinLevel = LogLevel.Info; o.WriteTo.ColoredConsole(); });
var logger = container.CreateScope().GetService<ILogger<Program>>();
```

---

## Packages

| Package | Description |
|---|---|
| **PicoDI** | Zero-reflection DI container |
| **PicoDI.Abs** | DI abstractions |
| **PicoDI.Gen** | Compile-time registration source generator |
| **PicoAop.Abs** | Interceptor abstractions (legacy) |
| **PicoAop.Gen** | Decorator + invocation source generator (legacy) |
| **PicoAop.DI** | DI integration for PicoAop (legacy) |
| **PicoAop.Abs** | **Next-gen** AOT-first interceptor abstractions — zero boxing, zero allocation |
| **PicoAop.Gen** | Compile-time Invocation struct + proxy class generation |
| **PicoCfg** | Async-first configuration |
| **PicoCfg.Abs** | Configuration abstractions |
| **PicoCfg.Gen** | Typed binding source generator |
| **PicoCfg.DI** | DI integration for PicoCfg |
| **PicoLog** | Structured logging |
| **PicoLog.Abs** | Logging abstractions |
| **PicoLog.Gen** | `[PicoLogMessage]` source generator |
| **PicoLog.DI** | DI integration for PicoLog |
| **PicoMediator** | Compile-time request/notification dispatch |
| **PicoMediator.Abs** | Mediator abstractions (ISender/IPublisher/IMediator) |
| **PicoMediator.Gen** | Handler → switch dispatch source generator |
| **PicoMediator.DI** | DI integration for PicoMediator |

---

## Design Philosophy

**克制 (Restraint)** — DI, Config, Logging, AOP, Mediator. The common infrastructure every app needs. No Web, no ORM, no message queue.

**专注 (Focus)** — Each module does one thing. Deep specialization over shallow generality.

**优雅 (Elegance)** — `new SvcContainer()` replaces pages of `Host.CreateDefaultBuilder()` ceremony. Source generators handle wiring at compile time.

**高效 (Efficiency)** — AOT First. Zero reflection. Everything resolvable at compile time is resolved at compile time.

---

## Learn More

- [PicoDI](PicoDI/README.md) — DI container, registration, source generator
- [PicoAop](PicoAop/README.md) — Compile-time decorator generation (legacy)
- [PicoAot](PicoAop/README.md) — Next-gen AOT-first interception (supersedes PicoAop)
- [PicoCfg](PicoCfg/README.md) — Configuration providers, binding, file watching
- [PicoLog](PicoLog/README.md) — Structured logging, sinks, message templates
- [PicoMediator](PicoMediator/README.md) — Request/notification dispatch
- [Contributing](CONTRIBUTING.md)
- [Security](SECURITY.md)

---

MIT License. [https://github.com/PicoHex/PicoHex](https://github.com/PicoHex/PicoHex)
