# PicoHex

**AOT First 的 .NET 通用最小基础设施**

Die minimale universelle Infrastruktur für produktionsreife .NET-Anwendungen &mdash; drei Module, elf Pakete, null Laufzeit-Reflection.

[![NuGet](https://img.shields.io/nuget/v/PicoDI)](https://nuget.org/packages/PicoDI)
[![License: MIT](https://img.shields.io/badge/License-MIT-green.svg)](LICENSE)
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

## Leistung

Benchmarks ausgeführt auf **.NET 10.0.5, Windows 10, X64, Native AOT, Release-Modus**.

### PicoDI vs Microsoft.Extensions.DependencyInjection

**20/20 Siege, durchschnittlich 2,83&times; schneller, max. 4,00&times; (DeepChain &times; Transient).**

| Szenario | PicoDI (ns) | MsDI (ns) | Beschleunigung |
|---|---|---|---|
| **DeepChain &times; Transient** | 156.7 | 626.9 | **4.00&times;** |
| **NoDependency &times; Singleton** | 14.2 | 55.1 | **3.89&times;** |
| **MultipleResolutions &times; Singleton** | 1,523.6 | 5,373.9 | **3.53&times;** |
| **MultipleResolutions &times; Transient** | 4,672.4 | 16,402.1 | **3.51&times;** |
| **NoDependency &times; Transient** | 27.7 | 97.1 | **3.50&times;** |
| ContainerSetup | 739.9 | 1,919.1 | 2.59&times; |
| SingleResolution &times; Transient | 55.3 | 187.8 | 3.39&times; |
| ScopeCreation | 94.1 | 104.5 | 1.11&times; |

### PicoCfg vs Microsoft.Extensions.Configuration

**1.35&times;&ndash;1.57&times; schneller bei gemischten Workloads.**

| Szenario | PicoCfg (ns) | MsConfig (ns) | Beschleunigung |
|---|---|---|---|
| Mixed n=100, p=2, l=1 | 5,920.9 | 9,273.9 | **1.57&times;** |
| Mixed n=100, p=2, l=10 | 29,831.8 | 40,218.9 | **1.35&times;** |

### PicoLog vs Microsoft.Extensions.Logging

PicoLog erzeugt umfangreichere Logeinträge (Zeitstempel, Kategorie, Scopes, Eigenschaften), weshalb der asynchrone Übergabepfad mit dem schlanken String-Kanal von Microsoft vergleichbar ist. Die **Kontroll-Benchmarks** messen die zugrunde liegende Effizienz ohne Queue-/Sink-Overhead:

| Kontroll-Benchmark | N=1 | N=10 | N=100 |
|---|---|---|---|
| **TimestampNowOnly** | 4.15&times; | 5.05&times; | 4.54&times; |
| **LogEntryAllocateOnly** | 8.48&times; | 23.48&times; | 20.11&times; |

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

var svc = container.CreateScope().GetService<IService>();
```

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

---

## PicoDI

Reflection-freier DI-Container mit Codegenerierung zur Kompilierzeit.

### Registrierung

Alle `Register*`-Methoden geben `ISvcContainer` für flüssige Verkettung zurück.

```csharp
var container = new SvcContainer();

// Factory-basiert (funktioniert immer, kein Source Generator nötig)
container.RegisterSingleton<IService>(scope => new Service(scope.GetService<IDep>()));
container.RegisterScoped<IRepository>(scope => new Repository());
container.RegisterTransient<IValidator>(scope => new Validator());

// Fertige Instanz
container.RegisterSingle<IClock>(SystemClock.Instance);

// Offene Generics
container.Register(typeof(IRepository<>), typeof(SqlRepository<>), SvcLifetime.Scoped);

// Typ-basiert (erfordert PicoDI.Gen Source Generator)
container.RegisterSingleton<IService, Service>();

// Gehostete Dienste
container.RegisterHostedSvc<BackgroundWorker>();

container.Build();  // Registrierungen einfrieren
```

### Auflösung

```csharp
using var scope = container.CreateScope();

// Typisierte Auflösung (ohne Lookup dank generierter Resolve.*-Methoden)
var svc = scope.GetService<IService>();
var repos = scope.GetServices<IRepository>();

// Typ-basierte Auflösung
var instance = scope.GetService(typeof(IService));
```

### Lebenszyklen

| Lebensdauer | Instanziierung | Freigabe |
|---|---|---|
| **Transient** | Bei jedem Aufruf neu | Vom auflösenden Scope verfolgt, in LIFO-Reihenfolge freigegeben |
| **Scoped** | Einmal pro Scope | Freigegeben, wenn der Scope freigegeben wird |
| **Singleton** | Einmal pro Container | Freigegeben, wenn der Container freigegeben wird |

### Source Generator (PicoDI.Gen)

Fügen Sie `PicoDI.Gen` als Analyzer hinzu, um typbasierte Registrierungen zur Kompilierzeit zu aktivieren:

```xml
<PackageReference Include="PicoDI.Gen" PrivateAssets="all" />
```

Der Generator durchsucht alle `Register*`-Aufrufe und erzeugt:

- **`ConfigureGeneratedServices()`**-Erweiterungsmethode mit Inline-Factory-Delegaten (keine Reflection)
- **Typisierte `Resolve.*`-Methoden** für Auflösungspfade ohne Lookup
- **Erkennung zirkulärer Abhängigkeiten zur Kompilierzeit**
- **Metadaten für offene Generics** zur projektübergreifenden Entdeckung
- **`[ModuleInitializer]`**-Autokonfigurator, der sich bei `SvcContainerAutoConfiguration` registriert

**Vorher** (Ihr Code):
```csharp
var container = new SvcContainer();
container.RegisterSingleton<IService, Service>();
container.Build();
```

**Nachher** (generiert):
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

Optionaler, flüssiger Builder für gehostete Dienste:

```csharp
using var hostBuilder = new SvcHostBuilder();
var host = await hostBuilder
    .ConfigureServices(container =>
    {
        container.RegisterSingleton<IService>(_ => new Service());
        container.RegisterHostedSvc<Worker>();
    })
    .BuildAsync();

// Anwendung läuft bis zum Stopp
await host.StopAsync();
```

---

## PicoCfg

Asynchron-first Konfigurationsverwaltung mit Provider-Modell.

### Provider-Modell

```
Sources ──→ Providers ──→ Root ──→ Consumer
```

Quellen definieren **wie** Konfiguration erzeugt wird. Provider sind die materialisierten Instanzen. Der Root setzt Provider-Snapshots zu einer einheitlichen Sicht zusammen. Verbraucher fragen via `TryGetValue` oder `GetValue` ab.

### Integrierte Quellen

| Quelle | Beschreibung |
|---|---|
| **Dictionary** | Schlüssel-Wert-Paare im Arbeitsspeicher |
| **Umgebungsvariablen** | Betriebssystem-Umgebung, Präfix-Filterung, `__` &rarr; `:`-Mapping |
| **Kommandozeile** | `--key=value`, `--key value`, `-key value`, `/key value` |
| **Stream** | Zeilenbasiertes `key=value`-Text-Parsing mit Dateiüberwachung |
| **Dateiüberwachung** | Automatisches Neuladen bei Dateiänderungen mit Entprellung |
| **Verkettet** | Fallback auf eine andere `ICfg`-Instanz |
| **KeyPerFile** | Kubernetes ConfigMap-Art &mdash; Dateiname=Schlüssel, Inhalt=Wert |

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

### Quellgenerierte Bindung

Fügen Sie `PicoCfg.Gen` als Analyzer hinzu. Der Source Generator entdeckt Bindeziele zur Kompilierzeit anhand von `CfgBind.Bind<T>`-/`CfgBind.TryBind<T>`-/`CfgBind.BindInto<T>`-Aufrufstellen &mdash; keine Attribute erforderlich. Der generierte Code registriert stark typisierte Binder, die Konfigurationswerte über Schlüsselpfade ohne Laufzeit-Reflection auflösen.

```csharp
using PicoCfg;

public sealed class AppSettings
{
    public string Name { get; init; }
    public int MaxRetries { get; init; } = 3;
    public bool EnableFeature { get; init; }
}

// Aufrufstelle löst Source Generation für AppSettings aus
var settings = CfgBind.Bind<AppSettings>(cfg, "App");
```

---

## PicoLog

Strukturiertes Logging mit Nachrichtenvorlagen zur Kompilierzeit.

### Log-Level

| Level | Wert | Verwendung |
|---|---|---|
| **Emergency** | 0 | System nicht mehr nutzbar |
| **Alert** | 1 | Sofortiges Handeln erforderlich |
| **Critical** | 2 | Kritische Bedingungen |
| **Error** | 3 | Fehlerbedingungen |
| **Warning** | 4 | Warnbedingungen |
| **Notice** | 5 | Normal, aber signifikant |
| **Info** | 6 | Informationsmeldungen |
| **Debug** | 7 | Debug-Meldungen |
| **Trace** | 8 | Detaillierte Diagnoseablaufverfolgung |
| **None** | 255 | Deaktiviert sämtliches Logging |

### Nachrichtenvorlagen

Unterstützung sowohl für Zeichenketteninterpolation als auch `FormattableString` mit benannten Parametern für strukturiertes Logging:

```csharp
logger.Info($"Processing order {orderId} for {customer}");
logger.Log(LogLevel.Info, $"User {user} logged in from {ipAddress}");

// Strukturiertes Logging mit expliziten Eigenschaften
logger.LogStructured(LogLevel.Error, "Payment failed",
    properties: [new("OrderId", orderId), new("Amount", amount)],
    exception: ex);
```

### Quellgenerierte Nachrichten

Das `[PicoLogMessage]`-Attribut auf statischen partiellen Methoden erzeugt stark typisierte, AOT-kompatible Erweiterungsmethoden:

```csharp
public static partial class AppLogs
{
    [PicoLogMessage(LogLevel.Info, EventId = 1001, Message = "Order {OrderId} placed")]
    public static partial void OrderPlaced(this ILogger logger, string orderId);

    [PicoLogMessage(LogLevel.Error, EventId = 2001, Message = "Payment failed")]
    public static partial void PaymentFailed(this ILogger logger, string orderId, Exception ex);
}

// Verwendung
logger.OrderPlaced("ORD-12345");
logger.PaymentFailed("ORD-12345", ex);
```

### Integrierte Senken

```csharp
var sinks = new List<ILogSink>
{
    new ConsoleSink(new ConsoleFormatter()),          // Reine Konsole
    new ColoredConsoleSink(new ConsoleFormatter()),    // Farbcodiert nach Level
    new FileSink(new ConsoleFormatter(),               // Gepufferte Dateiausgabe
        new FileSinkOptions { FilePath = "app.log", BatchSize = 100 })
};
using var factory = new LoggerFactory(sinks);
```

### Eigene Senken

```csharp
public sealed class CustomSink : ILogSink
{
    public Task WriteAsync(LogEntry entry, CancellationToken ct = default)
    {
        // In Ihr Backend schreiben
        return Task.CompletedTask;
    }

    public void Dispose() { }
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
```

---

## Modul-Integration

Wenn `PicoDI`, `PicoCfg.DI` und `PicoLog.DI` alle referenziert werden, erfolgt die Verdrahtung automatisch:

1. Der Source Generator jedes Moduls registriert einen `ModuleInitializer`-basierten Konfigurator
2. `new SvcContainer()` führt alle Konfiguratoren via `SvcContainerAutoConfiguration.TryApplyConfiguration` aus
3. Keine manuelle Verdrahtung nötig

### DI-Integrations-APIs

**PicoCfg.DI** &mdash; Erweiterungen auf `ISvcContainer`:
- `RegisterCfgRoot(ICfgRoot root)` &mdash; registriert `ICfgRoot` und `ICfg`
- `RegisterCfgTransient/Scoped/Singleton<T>(string? section)` &mdash; POCOs aus Konfiguration binden
- `RegisterCfgOptionsSingleton/Scoped<T>(string? section)` &mdash; typisierte Optionsunterstützung

**PicoLog.DI** &mdash; Erweiterung auf `ISvcContainer`:
- `AddPicoLog(Action<LoggingOptions> configure)` &mdash; registriert `ILoggerFactory` und `ILogger<>`
- `WriteTo.Console()` / `.ColoredConsole()` / `.File(path)` / `.Sink(custom)` &mdash; Senkenkonfiguration
- `ReadFrom.RegisteredSinks()` &mdash; DI-registrierte `ILogSink`-Instanzen einbeziehen

### Minimales kombiniertes Beispiel

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

## Design-Philosophie

**克制 (Restraint)** — Nur DI, Config, Logging. Kein Web-Framework, kein ORM, keine Message Queue. Was nicht zur universellen Infrastruktur gehört, hat hier nichts verloren. PicoHex ist der kleinste gemeinsame Nenner, den jede App braucht &mdash; nicht mehr, nicht weniger.

**专注 (Focus)** — Jedes Modul macht genau eine Sache. PicoDI ist ein Container, kein Service Locator. PicoCfg ist Konfiguration, kein Feature-Flag-System. PicoLog ist Logging, keine Telemetrie-Pipeline. Tiefe Spezialisierung statt oberflächlicher Allgemeinheit.

**优雅 (Elegance)** — Die APIs sind minimal. Source Generators erledigen die Verdrahtung zur Kompilierzeit. Der Entwickler schreibt geradlinigen Code; die Werkzeuge handhaben die Komplexität. `new SvcContainer()` ersetzt 100+ Zeilen `Host.CreateDefaultBuilder()`-Zeremonie.

**高效 (Efficiency)** — AOT First ist kein nachträglicher Einfall &mdash; es ist das Fundament. Keine Reflection. Kein Laufzeit-Overhead. Alles, was zur Kompilierzeit aufgelöst werden kann, wird zur Kompilierzeit aufgelöst. Minimale Binaries, schnelle Kaltstarts, vorhersagbare Leistung.

---

## Anwendungsfälle

| Szenario | PicoDI | PicoCfg | PicoLog | Warum |
|---|---|---|---|---|
| **CLI-Werkzeuge** | Optional | Essenziell | Essenziell | Argumente/Konfiguration parsen, Ausgabepräzision steuern. DI steht bereit, falls das Werkzeug wächst. |
| **Serverless / Lambda** | Essenziell | Essenziell | Essenziell | Kaltstart ist der Engpass. AOT-kompilierte DI mit vorgenerierten Auflösungspfaden. |
| **WASM / Blazor** | Essenziell | Essenziell | Essenziell | Download-Größe zählt. 11 Pakete, trim-freundlich, keine Laufzeit-Aufblähung. |
| **Embedded / IoT** | Bei Bedarf | Essenziell | Essenziell | Ressourcenbeschränkte Geräte. Minimales Binary, wo möglich keine Allokationen. |

---

## Pakete

| Paket | Beschreibung | NuGet |
|---|---|---|
| **PicoDI** | Reflection-freier DI-Container mit Codegenerierung zur Kompilierzeit | [![NuGet](https://img.shields.io/nuget/v/PicoDI)](https://nuget.org/packages/PicoDI) |
| **PicoDI.Abs** | Abstraktionen für PicoDI (`ISvcContainer`, `ISvcScope`, `SvcDescriptor`) | [![NuGet](https://img.shields.io/nuget/v/PicoDI.Abs)](https://nuget.org/packages/PicoDI.Abs) |
| **PicoDI.Gen** | Roslyn Source Generator &mdash; Registrierung und Auflösung zur Kompilierzeit | [![NuGet](https://img.shields.io/nuget/v/PicoDI.Gen)](https://nuget.org/packages/PicoDI.Gen) |
| **PicoCfg** | Asynchron-first Konfigurationsverwaltung mit Provider-Modell | [![NuGet](https://img.shields.io/nuget/v/PicoCfg)](https://nuget.org/packages/PicoCfg) |
| **PicoCfg.Abs** | Abstraktionen für PicoCfg (`ICfg`, `ICfgRoot`, `ICfgSection`) | [![NuGet](https://img.shields.io/nuget/v/PicoCfg.Abs)](https://nuget.org/packages/PicoCfg.Abs) |
| **PicoCfg.Gen** | Source Generator für typisierte Konfigurationsbindung (`CfgBind.Bind<T>`) | [![NuGet](https://img.shields.io/nuget/v/PicoCfg.Gen)](https://nuget.org/packages/PicoCfg.Gen) |
| **PicoCfg.DI** | PicoDI-Integration für PicoCfg (`RegisterCfgRoot`, `RegisterCfgSingleton`) | [![NuGet](https://img.shields.io/nuget/v/PicoCfg.DI)](https://nuget.org/packages/PicoCfg.DI) |
| **PicoLog** | Strukturiertes Logging mit Nachrichtenvorlagen zur Kompilierzeit | [![NuGet](https://img.shields.io/nuget/v/PicoLog)](https://nuget.org/packages/PicoLog) |
| **PicoLog.Abs** | Abstraktionen für PicoLog (`ILogger`, `ILogSink`, `LogLevel`) | [![NuGet](https://img.shields.io/nuget/v/PicoLog.Abs)](https://nuget.org/packages/PicoLog.Abs) |
| **PicoLog.Gen** | Source Generator für `[PicoLogMessage]`-Methoden | [![NuGet](https://img.shields.io/nuget/v/PicoLog.Gen)](https://nuget.org/packages/PicoLog.Gen) |
| **PicoLog.DI** | PicoDI-Integration für PicoLog (`AddPicoLog`, `WriteTo`, `ReadFrom`) | [![NuGet](https://img.shields.io/nuget/v/PicoLog.DI)](https://nuget.org/packages/PicoLog.DI) |

---

## Vergleich

### vs Autofac

Autofac ist ein ausgereifter, funktionsreicher DI-Container mit Property Injection, Decorators, Interceptors und Modulen. PicoDI verfolgt den gegenteiligen Ansatz: **keine Laufzeit-Reflection, ausschließliche Registrierung zur Kompilierzeit, von Haus aus AOT-sicher**. Wenn Sie Laufzeitflexibilität brauchen, nehmen Sie Autofac. Wenn Sie **AOT und minimalen Overhead** brauchen, nehmen Sie PicoDI.

### vs Lamar

Lamar ist ein leistungsfähiger DI-Container, der Codegenerierung zur Laufzeit einsetzt (`DynamicAssembly` + IL-Emit). PicoDI nutzt Roslyn Source Generators für Codegenerierung zur **Kompilierzeit**. Lamar unterstützt mehr Features (Interception, Decoration); PicoDI ist minimalistisch und AOT-first.

### vs Serilog

Serilog ist der Goldstandard für strukturiertes Logging in .NET mit einem riesigen Senken-Ökosystem. PicoLog ist kein Serilog-Ersatz &mdash; es ist eine **leichtgewichtige Alternative** für Projekte, die AOT-Kompatibilität und minimale Abhängigkeiten über Senkenvielfalt stellen. Der Source Generator von PicoLog für Nachrichtenvorlagen liefert vergleichbare Qualität beim strukturierten Logging.

### vs Microsoft.Extensions

PicoHex ist keine Erweiterung von `Microsoft.Extensions` &mdash; es ist eine **Alternative**. Von Grund auf für Native AOT entwickelt, mit Codegenerierung zur Kompilierzeit statt Laufzeit-Reflection. Wenn Sie traditionelle ASP.NET-Anwendungen bauen, bleiben Sie bei Microsoft.Extensions. Wenn Sie **CLI-Werkzeuge, Serverless-Funktionen, WASM-Apps oder eingebettete Systeme** bauen, ist PicoHex für Ihren Anwendungsfall gemacht.

---

## Lizenz &amp; Mitwirkung

MIT-Lizenz. Repository: [https://github.com/PicoHex/PicoHex](https://github.com/PicoHex/PicoHex)
