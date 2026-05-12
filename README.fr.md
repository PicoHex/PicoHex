# PicoHex

**AOT First 的 .NET 通用最小基础设施**

Infrastructure minimale universelle pour les applications .NET de qualité production &mdash; trois modules, onze paquets, zéro réflexion à l'exécution.

[![NuGet](https://img.shields.io/nuget/v/PicoDI)](https://nuget.org/packages/PicoDI)
[![License: MIT](https://img.shields.io/badge/License-MIT-green.svg)](LICENSE)
[![CI](https://github.com/PicoHex/PicoHex/actions/workflows/ci.yml/badge.svg)](https://github.com/PicoHex/PicoHex/actions/workflows/ci.yml)



[English](README.md) | [简体中文](README.zh.md) | [日本語](README.ja.md) | [Español](README.es.md) | [Português](README.pt.md) | [繁體中文](README.zh-tw.md) | [한국어](README.ko.md) | [Français](README.fr.md) | [Deutsch](README.de.md) | [Русский](README.ru.md)

---

## Modèle de calcul

```
Configuration  ──→  Injection de dépendances  ──→  Journalisation
  (Entrée)              (Cœur)                      (Sortie)
```

Toute application lit sa configuration, assemble ses composants internes et produit des résultats. **PicoHex** est l'implémentation minimale de ces trois méta-opérations pour .NET Native AOT &mdash; conçue de zéro pour la génération de code à la compilation plutôt que la réflexion à l'exécution.

---

## Pourquoi PicoHex

| | Microsoft.Extensions | PicoHex |
|---|---|---|
| **Paquets** | 40+ | 11 |
| **Réflexion runtime** | Intensive (`Activator.CreateInstance`, arbres d'expression) | Zéro (générateurs de source) |
| **Prêt pour AOT** | Opt-in requis, configuration minutieuse | AOT First &mdash; compile nativement sans effort |
| **HostBuilder nécessaire** | Oui &mdash; indispensable pour câbler DI + Config + Logging | Non &mdash; `new SvcContainer()` suffit |
| **Intégration des modules** | Enregistrement runtime (`IServiceCollection`) | À la compilation via `ModuleInitializer` + générateurs de source |
| **Démarrage à froid** | Lent (JIT + réflexion) | Rapide (chemins d'exécution pré-générés) |
| **Taille du binaire** | Grande (nombreux assemblies importés) | Minimale (éliminable, compatible linker) |

---

## Performances

Les benchmarks sont exécutés sur **.NET 10.0.5, Windows 10, X64, Native AOT, mode Release**.

### PicoDI vs Microsoft.Extensions.DependencyInjection

**20 victoires sur 20, 2,83&times; plus rapide en moyenne, max 4,00&times; (DeepChain &times; Transient).**

| Scénario | PicoDI (ns) | MsDI (ns) | Accélération |
|---|---|---|---|
| **DeepChain &times; Transient** | 156,7 | 626,9 | **4,00&times;** |
| **NoDependency &times; Singleton** | 14,2 | 55,1 | **3,89&times;** |
| **MultipleResolutions &times; Singleton** | 1 523,6 | 5 373,9 | **3,53&times;** |
| **MultipleResolutions &times; Transient** | 4 672,4 | 16 402,1 | **3,51&times;** |
| **NoDependency &times; Transient** | 27,7 | 97,1 | **3,50&times;** |
| ContainerSetup | 739,9 | 1 919,1 | 2,59&times; |
| SingleResolution &times; Transient | 55,3 | 187,8 | 3,39&times; |
| ScopeCreation | 94,1 | 104,5 | 1,11&times; |

**Binaire AOT : 3 087,5 Ko**

### PicoCfg vs Microsoft.Extensions.Configuration

**De 1,35&times; à 1,57&times; plus rapide en charge mixte.**

| Scénario | PicoCfg (ns) | MsConfig (ns) | Accélération |
|---|---|---|---|
| Mixte n=100, p=2, l=1 | 5 920,9 | 9 273,9 | **1,57&times;** |
| Mixte n=100, p=2, l=10 | 29 831,8 | 40 218,9 | **1,35&times;** |

**Binaire AOT : 2 476,5 Ko**

### PicoLog vs Microsoft.Extensions.Logging

PicoLog construit des entrées de journal plus riches (horodatage, catégorie, portées, propriétés), ce qui explique pourquoi le chemin de passage asynchrone est comparable au transfert léger par chaîne de caractères de Microsoft. Les **benchmarks de contrôle** mesurent l'efficacité sous-jacente sans la surcharge de la file d'attente et du récepteur :

| Benchmark de contrôle | N=1 | N=10 | N=100 |
|---|---|---|---|
| **TimestampNowOnly** | 4,15&times; | 5,05&times; | 4,54&times; |
| **LogEntryAllocateOnly** | 8,48&times; | 23,48&times; | 20,11&times; |

**Binaire AOT : 3 020 Ko**

---

## Prise en main rapide

### Configuration uniquement

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

### Injection de dépendances uniquement

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

### Journalisation uniquement

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

Conteneur DI sans réflexion avec génération de code à la compilation.

### Enregistrement

Toutes les méthodes `Register*` retournent `ISvcContainer` pour un enchaînement fluide.

```csharp
var container = new SvcContainer();

// Par fabrique (fonctionne toujours, sans générateur de source)
container.RegisterSingleton<IService>(scope => new Service(scope.GetService<IDep>()));
container.RegisterScoped<IRepository>(scope => new Repository());
container.RegisterTransient<IValidator>(scope => new Validator());

// Instance pré-construite
container.RegisterSingle<IClock>(SystemClock.Instance);

// Génériques ouverts
container.Register(typeof(IRepository<>), typeof(SqlRepository<>), SvcLifetime.Scoped);

// Par type (nécessite le générateur de source PicoDI.Gen)
container.RegisterSingleton<IService, Service>();

// Services hébergés
container.RegisterHostedSvc<BackgroundWorker>();

container.Build();  // Geler les enregistrements
```

### Résolution

```csharp
using var scope = container.CreateScope();

// Résolution typée (recherche zéro via les méthodes Resolve.* générées)
var svc = scope.GetService<IService>();
var repos = scope.GetServices<IRepository>();

// Résolution par type
var instance = scope.GetService(typeof(IService));
```

### Durées de vie

| Durée de vie | Instanciation | Libération |
|---|---|---|
| **Transient** | Nouvelle à chaque fois | Suivie par la portée de résolution, libérée en ordre LIFO |
| **Scoped** | Une fois par portée | Libérée lorsque la portée est libérée |
| **Singleton** | Une fois par conteneur | Libérée lorsque le conteneur est libéré |

### Générateur de source (PicoDI.Gen)

Ajoutez `PicoDI.Gen` comme analyseur pour activer les enregistrements par type à la compilation :

```xml
<PackageReference Include="PicoDI.Gen" PrivateAssets="all" />
```

Le générateur parcourt tous les appels `Register*` et émet :

- La méthode d'extension **`ConfigureGeneratedServices()`** avec des délégués fabrique en ligne (aucune réflexion)
- Des méthodes **typées `Resolve.*`** pour des chemins de résolution sans recherche
- La **détection de dépendances circulaires à la compilation**
- Les **métadonnées de génériques ouverts** pour la découverte entre assemblies
- Un **auto-configurateur `[ModuleInitializer]`** qui s'enregistre auprès de `SvcContainerAutoConfiguration`

**Avant** (votre code) :
```csharp
var container = new SvcContainer();
container.RegisterSingleton<IService, Service>();
container.Build();
```

**Après** (généré) :
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

Constructeur fluent optionnel pour les services hébergés :

```csharp
using var hostBuilder = new SvcHostBuilder();
var host = await hostBuilder
    .ConfigureServices(container =>
    {
        container.RegisterSingleton<IService>(_ => new Service());
        container.RegisterHostedSvc<Worker>();
    })
    .BuildAsync();

// L'application s'exécute jusqu'à l'arrêt
await host.StopAsync();
```

---

## PicoCfg

Gestion de configuration asynchrone avec modèle de fournisseurs.

### Modèle de fournisseurs

```
Sources ──→ Fournisseurs ──→ Racine ──→ Consommateur
```

Les sources définissent **comment** la configuration est produite. Les fournisseurs sont les instances matérialisées. La racine compose les instantanés des fournisseurs en une vue unifiée. Les consommateurs interrogent via `TryGetValue` ou `GetValue`.

### Sources intégrées

| Source | Description |
|---|---|
| **Dictionary** | Paires clé-valeur en mémoire |
| **Variables d'environnement** | Environnement OS, filtrage par préfixe, correspondance `__` &rarr; `:` |
| **Ligne de commande** | `--key=value`, `--key value`, `-key value`, `/key value` |
| **Flux** | Analyse texte ligne par ligne `key=value` avec surveillance de fichier |
| **Surveillance de fichier** | Rechargement automatique au changement avec anti-rebond |
| **Chaîné** | Repli vers une autre instance `ICfg` |
| **KeyPerFile** | Style Kubernetes ConfigMap &mdash; nomfichier=clé, contenu=valeur |

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

### Liaison générée par source

Ajoutez `PicoCfg.Gen` comme analyseur. Le générateur de source découvre les cibles de liaison à la compilation à partir des sites d'appel `CfgBind.Bind<T>` / `CfgBind.TryBind<T>` / `CfgBind.BindInto<T>` &mdash; aucun attribut requis. Le code généré enregistre des lieurs fortement typés qui résolvent les valeurs de configuration par chemin de clé sans réflexion à l'exécution.

```csharp
using PicoCfg;

public sealed class AppSettings
{
    public string Name { get; init; }
    public int MaxRetries { get; init; } = 3;
    public bool EnableFeature { get; init; }
}

// Le site d'appel déclenche la génération de source pour AppSettings
var settings = CfgBind.Bind<AppSettings>(cfg, "App");
```

---

## PicoLog

Journalisation structurée avec modèles de message à la compilation.

### Niveaux de journalisation

| Niveau | Valeur | Utilisation |
|---|---|---|
| **Emergency** | 0 | Le système est inutilisable |
| **Alert** | 1 | Une action immédiate est requise |
| **Critical** | 2 | Conditions critiques |
| **Error** | 3 | Conditions d'erreur |
| **Warning** | 4 | Conditions d'avertissement |
| **Notice** | 5 | Normal mais significatif |
| **Info** | 6 | Messages informatifs |
| **Debug** | 7 | Messages de débogage |
| **Trace** | 8 | Traçage diagnostic détaillé |
| **None** | 255 | Désactive toute journalisation |

### Modèles de message

Prise en charge de l'interpolation de chaînes et de `FormattableString` avec paramètres nommés pour la journalisation structurée :

```csharp
logger.Info($"Processing order {orderId} for {customer}");
logger.Log(LogLevel.Info, $"User {user} logged in from {ipAddress}");

// Journalisation structurée avec propriétés explicites
logger.LogStructured(LogLevel.Error, "Payment failed",
    properties: [new("OrderId", orderId), new("Amount", amount)],
    exception: ex);
```

### Messages générés par source

L'attribut `[PicoLogMessage]` sur des méthodes partielles statiques émet des méthodes d'extension fortement typées et compatibles AOT :

```csharp
public static partial class AppLogs
{
    [PicoLogMessage(LogLevel.Info, EventId = 1001, Message = "Order {OrderId} placed")]
    public static partial void OrderPlaced(this ILogger logger, string orderId);

    [PicoLogMessage(LogLevel.Error, EventId = 2001, Message = "Payment failed")]
    public static partial void PaymentFailed(this ILogger logger, string orderId, Exception ex);
}

// Utilisation
logger.OrderPlaced("ORD-12345");
logger.PaymentFailed("ORD-12345", ex);
```

### Récepteurs intégrés

```csharp
var sinks = new List<ILogSink>
{
    new ConsoleSink(new ConsoleFormatter()),          // Console simple
    new ColoredConsoleSink(new ConsoleFormatter()),    // Code couleur par niveau
    new FileSink(new ConsoleFormatter(),               // Sortie fichier par lots
        new FileSinkOptions { FilePath = "app.log", BatchSize = 100 })
};
using var factory = new LoggerFactory(sinks);
```

### Récepteurs personnalisés

```csharp
public sealed class CustomSink : ILogSink
{
    public Task WriteAsync(LogEntry entry, CancellationToken ct = default)
    {
        // Écrire vers votre backend
        return Task.CompletedTask;
    }

    public void Dispose() { }
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
```

---

## Intégration des modules

Lorsque `PicoDI`, `PicoCfg.DI` et `PicoLog.DI` sont tous référencés, le câblage se fait automatiquement :

1. Le générateur de source de chaque module enregistre un configurateur basé sur `ModuleInitializer`
2. `new SvcContainer()` exécute tous les configurateurs via `SvcContainerAutoConfiguration.TryApplyConfiguration`
3. Aucun câblage manuel nécessaire

### API d'intégration DI

**PicoCfg.DI** &mdash; extensions sur `ISvcContainer` :
- `RegisterCfgRoot(ICfgRoot root)` &mdash; enregistre `ICfgRoot` et `ICfg`
- `RegisterCfgTransient/Scoped/Singleton<T>(string? section)` &mdash; lie des POCO depuis la configuration
- `RegisterCfgOptionsSingleton/Scoped<T>(string? section)` &mdash; prise en charge d'options typées

**PicoLog.DI** &mdash; extensions sur `ISvcContainer` :
- `AddPicoLog(Action<LoggingOptions> configure)` &mdash; enregistre `ILoggerFactory` et `ILogger<>`
- `WriteTo.Console()` / `.ColoredConsole()` / `.File(path)` / `.Sink(custom)` &mdash; configuration des récepteurs
- `ReadFrom.RegisteredSinks()` &mdash; inclut les instances `ILogSink` enregistrées dans le DI

### Exemple combiné minimal

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

## Philosophie de conception

**克制 (Restraint)** — Seulement DI, Config, Logging. Pas de framework Web, pas d'ORM, pas de file de messages. Si ce n'est pas une infrastructure universelle, ça n'a pas sa place. PicoHex est le dénominateur commun dont toute application a besoin &mdash; ni plus, ni moins.

**专注 (Focus)** — Chaque module fait une seule chose. PicoDI est un conteneur, pas un localisateur de services. PicoCfg est de la configuration, pas un système de drapeaux de fonctionnalités. PicoLog est de la journalisation, pas un pipeline de télémétrie. La spécialisation profonde plutôt que la généralité superficielle.

**优雅 (Elegance)** — Les API sont minimales. Les générateurs de source effectuent le câblage à la compilation. Le développeur écrit du code simple ; les outils gèrent la complexité. `new SvcContainer()` remplace 100+ lignes de cérémonie `Host.CreateDefaultBuilder()`.

**高效 (Efficiency)** — AOT First n'est pas une réflexion après coup &mdash; c'est le fondement. Zéro réflexion. Zéro surcharge à l'exécution. Tout ce qui peut être résolu à la compilation l'est à la compilation. Des binaires minimaux, des démarrages à froid rapides, des performances prévisibles.

---

## Cas d'utilisation

| Scénario | PicoDI | PicoCfg | PicoLog | Pourquoi |
|---|---|---|---|---|
| **Outils CLI** | Optionnel | Essentiel | Essentiel | Analyser les arguments/la configuration, contrôler la verbosité. DI est là si l'outil évolue. |
| **Serverless / Lambda** | Essentiel | Essentiel | Essentiel | Le démarrage à froid est le goulot d'étranglement. DI compilée en AOT avec chemins de résolution pré-générés. |
| **WASM / Blazor** | Essentiel | Essentiel | Essentiel | La taille du téléchargement compte. 11 paquets, compatible élimination, pas de gonflement runtime. |
| **Embarqué / IoT** | Quand nécessaire | Essentiel | Essentiel | Appareils à ressources limitées. Binaire minimal, zéro allocation quand possible. |

---

## Paquets

| Paquet | Description | NuGet |
|---|---|---|
| **PicoDI** | Conteneur DI sans réflexion avec génération de code à la compilation | [![NuGet](https://img.shields.io/nuget/v/PicoDI)](https://nuget.org/packages/PicoDI) |
| **PicoDI.Abs** | Abstractions pour PicoDI (`ISvcContainer`, `ISvcScope`, `SvcDescriptor`) | [![NuGet](https://img.shields.io/nuget/v/PicoDI.Abs)](https://nuget.org/packages/PicoDI.Abs) |
| **PicoDI.Gen** | Générateur de source Roslyn &mdash; enregistrement et résolution à la compilation | [![NuGet](https://img.shields.io/nuget/v/PicoDI.Gen)](https://nuget.org/packages/PicoDI.Gen) |
| **PicoCfg** | Gestion de configuration asynchrone avec modèle de fournisseurs | [![NuGet](https://img.shields.io/nuget/v/PicoCfg)](https://nuget.org/packages/PicoCfg) |
| **PicoCfg.Abs** | Abstractions pour PicoCfg (`ICfg`, `ICfgRoot`, `ICfgSection`) | [![NuGet](https://img.shields.io/nuget/v/PicoCfg.Abs)](https://nuget.org/packages/PicoCfg.Abs) |
| **PicoCfg.Gen** | Générateur de source pour la liaison typée de configuration (`CfgBind.Bind<T>`) | [![NuGet](https://img.shields.io/nuget/v/PicoCfg.Gen)](https://nuget.org/packages/PicoCfg.Gen) |
| **PicoCfg.DI** | Intégration PicoDI pour PicoCfg (`RegisterCfgRoot`, `RegisterCfgSingleton`) | [![NuGet](https://img.shields.io/nuget/v/PicoCfg.DI)](https://nuget.org/packages/PicoCfg.DI) |
| **PicoLog** | Journalisation structurée avec modèles de message à la compilation | [![NuGet](https://img.shields.io/nuget/v/PicoLog)](https://nuget.org/packages/PicoLog) |
| **PicoLog.Abs** | Abstractions pour PicoLog (`ILogger`, `ILogSink`, `LogLevel`) | [![NuGet](https://img.shields.io/nuget/v/PicoLog.Abs)](https://nuget.org/packages/PicoLog.Abs) |
| **PicoLog.Gen** | Générateur de source pour les méthodes `[PicoLogMessage]` | [![NuGet](https://img.shields.io/nuget/v/PicoLog.Gen)](https://nuget.org/packages/PicoLog.Gen) |
| **PicoLog.DI** | Intégration PicoDI pour PicoLog (`AddPicoLog`, `WriteTo`, `ReadFrom`) | [![NuGet](https://img.shields.io/nuget/v/PicoLog.DI)](https://nuget.org/packages/PicoLog.DI) |

---

## Comparaison

### vs Autofac

Autofac est un conteneur DI mature et riche en fonctionnalités avec injection de propriétés, décorateurs, intercepteurs et modules. PicoDI adopte l'approche inverse : **zéro réflexion à l'exécution, enregistrement uniquement à la compilation, AOT-safe par conception**. Si vous avez besoin de flexibilité à l'exécution, utilisez Autofac. Si vous avez besoin d'**AOT et d'un encombrement minimal**, utilisez PicoDI.

### vs Lamar

Lamar est un conteneur DI performant qui utilise la génération de code à l'exécution (`DynamicAssembly` + émission IL). PicoDI utilise les générateurs de source Roslyn pour une génération de code **à la compilation**. Lamar prend en charge plus de fonctionnalités (interception, décoration) ; PicoDI est minimal et AOT-first.

### vs Serilog

Serilog est la référence en matière de journalisation structurée dans .NET avec un vaste écosystème de récepteurs. PicoLog n'est pas un remplacement de Serilog &mdash; c'est une **alternative légère** pour les projets qui privilégient la compatibilité AOT et les dépendances minimales plutôt que la variété des récepteurs. Le générateur de source de PicoLog pour les modèles de message offre une qualité de journalisation structurée comparable.

### vs Microsoft.Extensions

PicoHex n'est pas une extension de `Microsoft.Extensions` &mdash; c'est une **alternative**. Conçue de zéro pour Native AOT, avec génération de code à la compilation plutôt que réflexion à l'exécution. Si vous construisez des applications ASP.NET traditionnelles, restez avec Microsoft.Extensions. Si vous construisez des **outils CLI, des fonctions serverless, des applications WASM ou des systèmes embarqués**, PicoHex est fait pour votre cas d'utilisation.

---

## Licence &amp; Contribution

Sous licence MIT. Dépôt : [https://github.com/PicoHex/PicoHex](https://github.com/PicoHex/PicoHex)
