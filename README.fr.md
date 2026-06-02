# PicoHex

**AOT First 的 .NET 通用最小基础设施**

Infrastructure minimale universelle pour les applications .NET de qualité production &mdash; cinq modules, zéro réflexion à l'exécution.

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

## Prise en main rapide

### Configuration uniquement

```shell
dotnet add package PicoCfg
```

```csharp
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
var container = new SvcContainer();
container.RegisterSingleton<IService>(scope => new MyService());
container.Build();
await using var scope = container.CreateScope();
var svc = scope.GetService<IService>();
```

### Journalisation uniquement

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

### Tout ensemble

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
await using var scope = container.CreateScope();
var logger = scope.GetService<ILogger<Program>>();
```

PicoDI prend également en charge l'**AOP/les intercepteurs à la compilation** — chaînez .InterceptBy<TInterceptor>() après Register() et le générateur de source émet des classes décoratrices à la compilation. [En savoir plus →](PicoDI/README.md#interceptor--aop-compile-time-decorators)

---

## Paquets

| Paquet | Description |
|---|---|
| **PicoDI** | Conteneur DI sans réflexion |
| **PicoDI.Abs** | Abstractions DI |
| **PicoDI.Gen** | Générateur de source d'enregistrement à la compilation |
| **PicoCfg** | Configuration asynchrone |
| **PicoCfg.Abs** | Abstractions de configuration |
| **PicoCfg.Gen** | Générateur de source pour la liaison typée |
| **PicoCfg.DI** | Intégration DI pour PicoCfg |
| **PicoLog** | Journalisation structurée |
| **PicoLog.Abs** | Abstractions de journalisation |
| **PicoLog.Gen** | Générateur de source `[PicoLogMessage]` |
| **PicoLog.DI** | Intégration DI pour PicoLog |

---

## Philosophie de conception

**克制 (Retenue)** — Seulement DI, Config, Logging. Pas de framework Web, pas d'ORM, pas de file de messages. Le dénominateur commun dont toute application a besoin.

**专注 (Concentration)** — Chaque module fait une seule chose. La spécialisation profonde plutôt que la généralité superficielle.

**优雅 (Élégance)** — `new SvcContainer()` remplace des pages de cérémonie `Host.CreateDefaultBuilder()`. Les générateurs de source effectuent le câblage à la compilation.

**高效 (Efficacité)** — AOT First. Zéro réflexion. Tout ce qui peut être résolu à la compilation l'est à la compilation.

---

## En savoir plus

- [PicoDI](PicoDI/README.md) — Conteneur DI, enregistrement, générateur de source
- [PicoCfg](PicoCfg/README.md) — Fournisseurs de configuration, liaison, surveillance de fichier
- [PicoLog](PicoLog/README.md) — Journalisation structurée, récepteurs, modèles de message
- [Contributing](CONTRIBUTING.md)
- [Security](SECURITY.md)

---

Licence MIT. [https://github.com/PicoHex/PicoHex](https://github.com/PicoHex/PicoHex)