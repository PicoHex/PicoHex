# PicoHex

**AOT First 的 .NET 通用最小基础设施**

Die minimale universelle Infrastruktur für produktionsreife .NET-Anwendungen &mdash; drei Module, elf Pakete, null Laufzeit-Reflection.

[![NuGet](https://img.shields.io/nuget/v/PicoDI)](https://nuget.org/packages/PicoDI)
[![License: MIT](https://img.shields.io/badge/License-MIT-green.svg)](https://github.com/PicoHex/PicoHex/blob/main/LICENSE)
[![CI](https://github.com/PicoHex/PicoHex/actions/workflows/ci.yml/badge.svg)](https://github.com/PicoHex/PicoHex/actions/workflows/ci.yml)



[English](README.md) | [简体中文](README.zh.md) | [日本語](README.ja.md) | [Español](README.es.md) | [Português](README.pt.md) | [繁體中文](README.zh-tw.md) | [한국어](README.ko.md) | [Français](README.fr.md) | [Deutsch](README.de.md) | [Русский](README.ru.md)

---

## Rechenmodell

```
Configuration  ──→  Dependency Injection  ──→  Logging
  (Input)              (Core)                   (Output)
```

Jede Anwendung liest Konfiguration ein, setzt ihre internen Abhängigkeiten zusammen und erzeugt Ausgabe. **PicoHex** ist die minimale Implementierung dieser drei Meta-Operationen für .NET Native AOT &mdash; von Grund auf für Codegenerierung zur Kompilierzeit anstelle von Laufzeit-Reflection konzipiert.

---

## Warum PicoHex

| | Microsoft.Extensions | PicoHex |
|---|---|---|
| **Pakete** | 40+ | 11 |
| **Laufzeit-Reflection** | Umfangreich (`Activator.CreateInstance`, Expression Trees) | Keine (Source Generators) |
| **AOT-tauglich** | Erfordert Opt-in und sorgfältige Konfiguration | AOT First &mdash; kompiliert nativ out of the box |
| **HostBuilder nötig** | Ja &mdash; erforderlich, um DI + Config + Logging zu verdrahten | Nein &mdash; `new SvcContainer()` genügt |
| **Modul-Integration** | Zur Laufzeit (`IServiceCollection`) | Zur Kompilierzeit via `ModuleInitializer` + Source Generators |
| **Kaltstart** | Langsam (JIT + Reflection) | Schnell (vorgenerierte Codepfade) |
| **Binary-Größe** | Groß (zieht viele Assemblies) | Minimal (trimmbar, linker-freundlich) |

---

## Schnellstart

### Nur Konfiguration

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

### Nur DI

```shell
dotnet add package PicoDI
```

```csharp
using PicoDI;
using PicoDI.Abs;

var container = new SvcContainer();
container.RegisterSingleton<IService>(scope => new MyService());
container.Build();

await using var scope = container.CreateScope();
var svc = scope.GetService<IService>();
```

PicoDI unterstützt auch **AOP/Interceptoren zur Kompilierzeit** — verketten Sie .InterceptBy<TInterceptor>() nach Register() und der Quellgenerator erzeugt Decorator-Klassen zur Build-Zeit. [Mehr erfahren →](PicoDI/README.md#interceptor--aop-compile-time-decorators)

### Nur Logging

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

### Alles zusammen

```shell
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
    .AddCommandLine(args)
    .BuildAsync();
container.RegisterCfgRoot(cfg);
container.AddPicoLog(o => { o.MinLevel = LogLevel.Info; o.WriteTo.ColoredConsole(); });
await using var scope = container.CreateScope();
var logger = scope.GetService<ILogger<Program>>();
```

PicoDI unterstützt auch **AOP/Interceptoren zur Kompilierzeit** — verketten Sie .InterceptBy<TInterceptor>() nach Register() und der Quellgenerator erzeugt Decorator-Klassen zur Build-Zeit. [Mehr erfahren →](PicoDI/README.md#interceptor--aop-compile-time-decorators)

---

## Pakete

| Paket | Beschreibung |
|---|---|
| **PicoDI** | Reflection-freier DI-Container |
| **PicoDI.Abs** | DI-Abstraktionen |
| **PicoDI.Gen** | Source Generator für Registrierung zur Kompilierzeit |
| **PicoCfg** | Asynchron-first Konfigurationsverwaltung |
| **PicoCfg.Abs** | Konfigurationsabstraktionen |
| **PicoCfg.Gen** | Source Generator für typisierte Bindung |
| **PicoCfg.DI** | DI-Integration für PicoCfg |
| **PicoLog** | Strukturiertes Logging |
| **PicoLog.Abs** | Logging-Abstraktionen |
| **PicoLog.Gen** | `[PicoLogMessage]` Source Generator |
| **PicoLog.DI** | DI-Integration für PicoLog |

---

## Design-Philosophie

**克制 (Restraint)** — Nur DI, Config, Logging. Kein Web-Framework, kein ORM, keine Message Queue. Was nicht zur universellen Infrastruktur gehört, hat hier nichts verloren. PicoHex ist der kleinste gemeinsame Nenner, den jede App braucht &mdash; nicht mehr, nicht weniger.

**专注 (Focus)** — Jedes Modul macht genau eine Sache. PicoDI ist ein Container, kein Service Locator. PicoCfg ist Konfiguration, kein Feature-Flag-System. PicoLog ist Logging, keine Telemetrie-Pipeline. Tiefe Spezialisierung statt oberflächlicher Allgemeinheit.

**优雅 (Elegance)** — Die APIs sind minimal. Source Generators erledigen die Verdrahtung zur Kompilierzeit. Der Entwickler schreibt geradlinigen Code; die Werkzeuge handhaben die Komplexität. `new SvcContainer()` ersetzt 100+ Zeilen `Host.CreateDefaultBuilder()`-Zeremonie.

**高效 (Efficiency)** — AOT First ist kein nachträglicher Einfall &mdash; es ist das Fundament. Keine Reflection. Kein Laufzeit-Overhead. Alles, was zur Kompilierzeit aufgelöst werden kann, wird zur Kompilierzeit aufgelöst. Minimale Binaries, schnelle Kaltstarts, vorhersagbare Leistung.

---

## Mehr erfahren

- [PicoDI](PicoDI/README.md) — DI-Container, Registrierung, Source Generator
- [PicoCfg](PicoCfg/README.md) — Konfigurationsanbieter, Bindung, Dateiüberwachung
- [PicoLog](PicoLog/README.md) — Strukturiertes Logging, Senken, Nachrichtenvorlagen
- [Mitwirken](CONTRIBUTING.md)
- [Sicherheit](SECURITY.md)

---

MIT-Lizenz. [https://github.com/PicoHex/PicoHex](https://github.com/PicoHex/PicoHex)