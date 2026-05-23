# PicoHex

**AOT-First Universal Minimal Infrastructure for .NET**

Three modules, eleven packages, zero runtime reflection.

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

Every application reads configuration, assembles its internals, and produces output. **PicoHex** is the minimal implementation of these three meta-operations for .NET Native AOT — designed from the ground up for compile-time code generation instead of runtime reflection.

---

## Why PicoHex

| | Microsoft.Extensions | PicoHex |
|---|---|---|
| **Packages** | 40+ | 11 |
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

PicoDI also supports **compile-time AOP/interceptors** — chain `.InterceptBy<TInterceptor>()` after `Register()` and the source generator emits decorator classes at build time. [Learn more →](PicoDI/README.md#interceptor--aop-compile-time-decorators)

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
| **PicoCfg** | Async-first configuration |
| **PicoCfg.Abs** | Configuration abstractions |
| **PicoCfg.Gen** | Typed binding source generator |
| **PicoCfg.DI** | DI integration for PicoCfg |
| **PicoLog** | Structured logging |
| **PicoLog.Abs** | Logging abstractions |
| **PicoLog.Gen** | `[PicoLogMessage]` source generator |
| **PicoLog.DI** | DI integration for PicoLog |

---

## Design Philosophy

**克制 (Restraint)** — Only DI, Config, Logging. No Web, no ORM, no message queue. The common denominator every app needs.

**专注 (Focus)** — Each module does one thing. Deep specialization over shallow generality.

**优雅 (Elegance)** — `new SvcContainer()` replaces 100+ lines of `Host.CreateDefaultBuilder()` ceremony. Source generators handle wiring at compile time.

**高效 (Efficiency)** — AOT First. Zero reflection. Everything resolvable at compile time is resolved at compile time.

---

## Learn More

- [PicoDI](PicoDI/README.md) — DI container, registration, source generator
- [PicoCfg](PicoCfg/README.md) — Configuration providers, binding, file watching
- [PicoLog](PicoLog/README.md) — Structured logging, sinks, message templates
- [Contributing](CONTRIBUTING.md)
- [Security](SECURITY.md)

---

MIT License. [https://github.com/PicoHex/PicoHex](https://github.com/PicoHex/PicoHex)
