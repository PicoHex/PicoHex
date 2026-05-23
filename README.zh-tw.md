# PicoHex

**AOT First 的 .NET 通用最小基礎設施**

生產級 .NET 應用程式的最小通用基礎設施 &mdash; 三個模組、十一個套件、零執行階段反射。

[![NuGet](https://img.shields.io/nuget/v/PicoDI)](https://nuget.org/packages/PicoDI)
[![License: MIT](https://img.shields.io/badge/License-MIT-green.svg)](LICENSE)
[![CI](https://github.com/PicoHex/PicoHex/actions/workflows/ci.yml/badge.svg)](https://github.com/PicoHex/PicoHex/actions/workflows/ci.yml)



[English](README.md) | [简体中文](README.zh.md) | [日本語](README.ja.md) | [Español](README.es.md) | [Português](README.pt.md) | [繁體中文](README.zh-tw.md) | [한국어](README.ko.md) | [Français](README.fr.md) | [Deutsch](README.de.md) | [Русский](README.ru.md)

---

## 計算模型

```
Configuration  ──→  Dependency Injection  ──→  Logging
  (輸入)              (核心)                   (輸出)
```

每個應用程式都需要讀取設定、組合內部元件、產出結果。**PicoHex** 是這三項後設操作在 .NET Native AOT 領域的最小化實作 &mdash; 從底層就為編譯期程式碼生成而設計，而非執行期反射。

---

## 為何選擇 PicoHex

| | Microsoft.Extensions | PicoHex |
|---|---|---|
| **套件數量** | 40+ | 11 |
| **執行期反射** | 大量使用 (`Activator.CreateInstance`、Expression trees) | 零反射（source generators） |
| **AOT 就緒** | 需手動啟用並謹慎設定 | AOT First &mdash; 開箱即用原生編譯 |
| **需要 HostBuilder** | 是 &mdash; 必須用它來串接 DI + Config + Logging | 否 &mdash; `new SvcContainer()` 就夠了 |
| **模組整合** | 執行期註冊 (`IServiceCollection`) | 編譯期透過 `ModuleInitializer` + source generators |
| **冷啟動** | 緩慢（JIT + reflection） | 快速（預先生成的程式碼路徑） |
| **二進位大小** | 龐大（引入大量組件） | 精簡（可修剪、對 linker 友善） |

---

## 快速開始

### 只要設定功能

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

### 只要 DI

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
```n
PicoDI 也支援**編譯期 AOP/攔截器**——在 Register() 後鏈式調用 .InterceptBy<TInterceptor>()，原始碼產生器會在編譯時產生裝飾器類別。[了解更多 →](PicoDI/README.md#interceptor--aop-compile-time-decorators)
```

### 只要日誌

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

### 全部一起用

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

---

## 套件列表

| 套件 | 說明 |
|---|---|
| **PicoDI** | 零反射的 DI 容器 |
| **PicoDI.Abs** | DI 抽象層 |
| **PicoDI.Gen** | 編譯期註冊原始碼生成器 |
| **PicoCfg** | 非同步優先的設定管理 |
| **PicoCfg.Abs** | 設定抽象層 |
| **PicoCfg.Gen** | 型別化綁定原始碼生成器 |
| **PicoCfg.DI** | PicoCfg 的 DI 整合 |
| **PicoLog** | 結構化日誌 |
| **PicoLog.Abs** | 日誌抽象層 |
| **PicoLog.Gen** | `[PicoLogMessage]` 原始碼生成器 |
| **PicoLog.DI** | PicoLog 的 DI 整合 |

---

## 設計理念

**克制 (Restraint)** — 只有 DI、Config、Logging。沒有 Web 框架、沒有 ORM、沒有訊息佇列。不屬於通用基礎設施的，就不該出現在這裡。PicoHex 是每個應用程式都需要的最小公分母 &mdash; 不多不少。

**專注 (Focus)** — 每個模組只做一件事。PicoDI 是一個容器，不是服務定位器。PicoCfg 是設定管理，不是功能開關系統。PicoLog 是日誌記錄，不是遙測管道。深耕專業，而非淺層通用。

**優雅 (Elegance)** — API 極簡。Source generators 在編譯期完成接線。開發者撰寫直觀的程式碼，工具處理複雜的部分。`new SvcContainer()` 取代了 100 多行 `Host.CreateDefaultBuilder()` 的繁文縟節。

**高效 (Efficiency)** — AOT First 不是事後補救 &mdash; 而是根本基石。零反射。零執行期開銷。所有能在編譯期決定的，就在編譯期決定。最小的二進位檔、快速的冷啟動、可預測的效能。

---

## 了解更多

- [PicoDI](PicoDI/README.md) &mdash; DI 容器、註冊、原始碼生成器
- [PicoCfg](PicoCfg/README.md) &mdash; 設定提供者、綁定、檔案監控
- [PicoLog](PicoLog/README.md) &mdash; 結構化日誌、接收器、訊息範本
- [貢獻指南](CONTRIBUTING.md)
- [安全政策](SECURITY.md)

---

MIT License. [https://github.com/PicoHex/PicoHex](https://github.com/PicoHex/PicoHex)
