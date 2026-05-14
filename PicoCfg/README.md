# PicoCfg

Async-first configuration management for .NET Native AOT.

## Quick Start

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

## Provider Model

```
Sources ──→ Providers ──→ Root ──→ Consumer
```

Sources define **how** config is produced. The root composes provider snapshots into a unified view. Consumers query via `TryGetValue` or `GetValue`.

## Built-in Sources

| Source | Description |
|---|---|
| **Dictionary** | In-memory key-value pairs |
| **Environment Variables** | OS environment, prefix filtering, `__` → `:` mapping |
| **Command Line** | `--key=value`, `--key value`, `-key value`, `/key value` |
| **Stream** | Line-based `key=value` text parsing with file watching |
| **File Watching** | Auto-reload on file change with debounce |
| **Chained** | Fallback to another `ICfg` instance |
| **KeyPerFile** | Kubernetes ConfigMap style — filename=key, content=value |

## CfgBuilder

```csharp
var cfg = await Cfg.CreateBuilder()
    .AddEnvironmentVariables("APP_")
    .Add(() => File.OpenRead("appsettings.cfg"))
    .Add(() => File.OpenRead($"appsettings.{env}.cfg"),
        watchPath: $"appsettings.{env}.cfg")
    .AddCommandLine(args)
    .BuildAsync();
```

## Source-Generated Binding

Add `PicoCfg.Gen` as an analyzer. Call `CfgBind.Bind<T>` to trigger compile-time binding generation.

```csharp
using PicoCfg;

public sealed class AppSettings
{
    public string Name { get; init; }
    public int MaxRetries { get; init; } = 3;
}

var settings = CfgBind.Bind<AppSettings>(cfg, "App");
```

## DI Integration (PicoCfg.DI)

```csharp
container.RegisterCfgRoot(root);                    // ICfgRoot + ICfg
container.RegisterCfgSingleton<AppSettings>("App"); // POCO from config
container.RegisterCfgOptionsSingleton<AppSettings>(); // typed options
```

## Packages

| Package | Description |
|---|---|
| **PicoCfg** | Configuration runtime |
| **PicoCfg.Abs** | `ICfg`, `ICfgRoot`, `ICfgSection` |
| **PicoCfg.Gen** | `CfgBind.Bind<T>` source generator |
| **PicoCfg.DI** | DI integration |

[← Back to PicoHex](../README.md)
