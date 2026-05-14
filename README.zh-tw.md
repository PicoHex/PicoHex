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

## 效能

基準測試環境：**.NET 10.0.5、Windows 10、X64、Native AOT、Release 模式**。

### PicoDI vs Microsoft.Extensions.DependencyInjection

**20/20 全勝，平均快 2.83&times;，最高快 4.00&times;（DeepChain &times; Transient）。**

| 情境 | PicoDI (ns) | MsDI (ns) | 加速比 |
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

**混合工作負載下快 1.35&times;&ndash;1.57&times;。**

| 情境 | PicoCfg (ns) | MsConfig (ns) | 加速比 |
|---|---|---|---|
| Mixed n=100, p=2, l=1 | 5,920.9 | 9,273.9 | **1.57&times;** |
| Mixed n=100, p=2, l=10 | 29,831.8 | 40,218.9 | **1.35&times;** |

### PicoLog vs Microsoft.Extensions.Logging

PicoLog 會構造更豐富的日誌条目（包含時間戳、類別、作用域、屬性），因此非同步交接路徑與 Microsoft 輕量級字串通道交接相當。**控制基準測試**則衡量排除佇列/接收器開銷後的底層效率：

| 控制基準測試 | N=1 | N=10 | N=100 |
|---|---|---|---|
| **TimestampNowOnly** | 4.15&times; | 5.05&times; | 4.54&times; |
| **LogEntryAllocateOnly** | 8.48&times; | 23.48&times; | 20.11&times; |

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

var svc = container.CreateScope().GetService<IService>();
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

---

## PicoDI

零反射的 DI 容器，搭配編譯期原始碼生成。

### 註冊

所有 `Register*` 方法皆回傳 `ISvcContainer`，支援流暢鏈式呼叫。

```csharp
var container = new SvcContainer();

// Factory 模式（永遠可用，不需要 source gen）
container.RegisterSingleton<IService>(scope => new Service(scope.GetService<IDep>()));
container.RegisterScoped<IRepository>(scope => new Repository());
container.RegisterTransient<IValidator>(scope => new Validator());

// 預建立的實例
container.RegisterSingle<IClock>(SystemClock.Instance);

// 開放泛型
container.Register(typeof(IRepository<>), typeof(SqlRepository<>), SvcLifetime.Scoped);

// 型別註冊（需搭配 PicoDI.Gen source generator）
container.RegisterSingleton<IService, Service>();

// 託管服務
container.RegisterHostedSvc<BackgroundWorker>();

container.Build();  // 凍結註冊
```

### 解析

```csharp
using var scope = container.CreateScope();

// 型別解析（透過生成的 Resolve.* 方法，零查詢開銷）
var svc = scope.GetService<IService>();
var repos = scope.GetServices<IRepository>();

// 基於型別的解析
var instance = scope.GetService(typeof(IService));
```

### 生命週期

| 生命週期 | 實例化 | 釋放 |
|---|---|---|
| **Transient** | 每次請求都建立新的 | 由解析時的作用域追蹤，以 LIFO 順序釋放 |
| **Scoped** | 每個作用域一次 | 作用域被釋放時一併釋放 |
| **Singleton** | 每個容器一次 | 容器被釋放時一併釋放 |

### Source Generator（PicoDI.Gen）

將 `PicoDI.Gen` 加入為分析器，即可啟用編譯期型別註冊：

```xml
<PackageReference Include="PicoDI.Gen" PrivateAssets="all" />
```

生成器會掃描所有 `Register*` 呼叫，並產出：

- **`ConfigureGeneratedServices()`** 擴充方法，內嵌 factory 委派（無反射）
- **型別安全的 `Resolve.*`** 方法，實現零查詢解析路徑
- **編譯期迴圈依賴偵測**
- **開放泛型中繼資料**，支援跨組件探索
- **`[ModuleInitializer]`** 自動設定器，向 `SvcContainerAutoConfiguration` 註冊

**Before**（你的程式碼）：
```csharp
var container = new SvcContainer();
container.RegisterSingleton<IService, Service>();
container.Build();
```

**After**（生成的程式碼）：
```csharp
// 由 PicoDI.Gen 自動生成
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

可選的流暢託管服務建構器：

```csharp
using var hostBuilder = new SvcHostBuilder();
var host = await hostBuilder
    .ConfigureServices(container =>
    {
        container.RegisterSingleton<IService>(_ => new Service());
        container.RegisterHostedSvc<Worker>();
    })
    .BuildAsync();

// 應用程式持續執行直到被停止
await host.StopAsync();
```

---

## PicoCfg

非同步優先的設定管理，採用提供者模型。

### 提供者模型

```
Sources ──→ Providers ──→ Root ──→ Consumer
```

**Sources** 定義設定的產生方式。**Providers** 是具體化的實例。**Root** 將各提供者的快照組合成統一視圖。**Consumer** 透過 `TryGetValue` 或 `GetValue` 查詢設定值。

### 內建來源

| 來源 | 說明 |
|---|---|
| **Dictionary** | 記憶體中的鍵值對 |
| **Environment Variables** | 作業系統環境變數，支援前綴過濾、`__` 轉 `:` 映射 |
| **Command Line** | `--key=value`、`--key value`、`-key value`、`/key value` |
| **Stream** | 基於行的 `key=value` 文字解析，支援檔案監聽 |
| **File Watching** | 檔案變更時自動重新載入，附帶防抖機制 |
| **Chained** | 備援至另一個 `ICfg` 實例 |
| **KeyPerFile** | Kubernetes ConfigMap 風格 &mdash; 檔名=鍵、內容=值 |

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

### Source Generated 綁定

將 `PicoCfg.Gen` 加入為分析器。Source generator 會從 `CfgBind.Bind<T>` / `CfgBind.TryBind<T>` / `CfgBind.BindInto<T>` 的呼叫處，在編譯期自動發現綁定目標 &mdash; 不需要任何屬性。生成的程式碼會註冊強型別綁定器，透過鍵路徑解析設定值，無需執行期反射。

```csharp
using PicoCfg;

public sealed class AppSettings
{
    public string Name { get; init; }
    public int MaxRetries { get; init; } = 3;
    public bool EnableFeature { get; init; }
}

// 呼叫處會觸發 AppSettings 的 source generation
var settings = CfgBind.Bind<AppSettings>(cfg, "App");
```

---

## PicoLog

結構化日誌，搭配編譯期訊息範本。

### 日誌層級

| 層級 | 數值 | 用途 |
|---|---|---|
| **Emergency** | 0 | 系統無法使用 |
| **Alert** | 1 | 必須立即採取行動 |
| **Critical** | 2 | 嚴重狀況 |
| **Error** | 3 | 錯誤狀況 |
| **Warning** | 4 | 警告狀況 |
| **Notice** | 5 | 正常但重要 |
| **Info** | 6 | 資訊性訊息 |
| **Debug** | 7 | 偵錯層級訊息 |
| **Trace** | 8 | 詳細診斷追蹤 |
| **None** | 255 | 停用所有日誌 |

### 訊息範本

同時支援字串插值和具名參數的 `FormattableString`，適用於結構化日誌：

```csharp
logger.Info($"Processing order {orderId} for {customer}");
logger.Log(LogLevel.Info, $"User {user} logged in from {ipAddress}");

// 明確屬性的結構化日誌
logger.LogStructured(LogLevel.Error, "Payment failed",
    properties: [new("OrderId", orderId), new("Amount", amount)],
    exception: ex);
```

### Source Generated 訊息

在靜態部分方法上使用 `[PicoLogMessage]` 屬性，即可產出強型別、AOT 相容的擴充方法：

```csharp
public static partial class AppLogs
{
    [PicoLogMessage(LogLevel.Info, EventId = 1001, Message = "Order {OrderId} placed")]
    public static partial void OrderPlaced(this ILogger logger, string orderId);

    [PicoLogMessage(LogLevel.Error, EventId = 2001, Message = "Payment failed")]
    public static partial void PaymentFailed(this ILogger logger, string orderId, Exception ex);
}

// 使用方式
logger.OrderPlaced("ORD-12345");
logger.PaymentFailed("ORD-12345", ex);
```

### 內建接收器

```csharp
var sinks = new List<ILogSink>
{
    new ConsoleSink(new ConsoleFormatter()),          // 純文字主控台
    new ColoredConsoleSink(new ConsoleFormatter()),    // 依層級著色
    new FileSink(new ConsoleFormatter(),               // 批次檔案輸出
        new FileSinkOptions { FilePath = "app.log", BatchSize = 100 })
};
using var factory = new LoggerFactory(sinks);
```

### 自訂接收器

```csharp
public sealed class CustomSink : ILogSink
{
    public Task WriteAsync(LogEntry entry, CancellationToken ct = default)
    {
        // 寫入你的後端服務
        return Task.CompletedTask;
    }

    public void Dispose() { }
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
```

---

## 模組整合

當同時參考 `PicoDI`、`PicoCfg.DI` 和 `PicoLog.DI` 時，接線會自動完成：

1. 每個模組的 source generator 註冊一個基於 `ModuleInitializer` 的設定器
2. `new SvcContainer()` 透過 `SvcContainerAutoConfiguration.TryApplyConfiguration` 執行所有設定器
3. 無需手動接線

### DI 整合 API

**PicoCfg.DI** &mdash; `ISvcContainer` 的擴充方法：
- `RegisterCfgRoot(ICfgRoot root)` &mdash; 註冊 `ICfgRoot` 和 `ICfg`
- `RegisterCfgTransient/Scoped/Singleton<T>(string? section)` &mdash; 從設定綁定 POCO
- `RegisterCfgOptionsSingleton/Scoped<T>(string? section)` &mdash; 型別化選項支援

**PicoLog.DI** &mdash; `ISvcContainer` 的擴充方法：
- `AddPicoLog(Action<LoggingOptions> configure)` &mdash; 註冊 `ILoggerFactory` 和 `ILogger<>`
- `WriteTo.Console()` / `.ColoredConsole()` / `.File(path)` / `.Sink(custom)` &mdash; 接收器設定
- `ReadFrom.RegisteredSinks()` &mdash; 納入 DI 註冊的 `ILogSink` 實例

### 最小化組合範例

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

## 設計理念

**克制 (Restraint)** — 只有 DI、Config、Logging。沒有 Web 框架、沒有 ORM、沒有訊息佇列。不屬於通用基礎設施的，就不該出現在這裡。PicoHex 是每個應用程式都需要的最小公分母 &mdash; 不多不少。

**專注 (Focus)** — 每個模組只做一件事。PicoDI 是一個容器，不是服務定位器。PicoCfg 是設定管理，不是功能開關系統。PicoLog 是日誌記錄，不是遙測管道。深耕專業，而非淺層通用。

**優雅 (Elegance)** — API 極簡。Source generators 在編譯期完成接線。開發者撰寫直觀的程式碼，工具處理複雜的部分。`new SvcContainer()` 取代了 100 多行 `Host.CreateDefaultBuilder()` 的繁文縟節。

**高效 (Efficiency)** — AOT First 不是事後補救 &mdash; 而是根本基石。零反射。零執行期開銷。所有能在編譯期決定的，就在編譯期決定。最小的二進位檔、快速的冷啟動、可預測的效能。

---

## 使用場景

| 情境 | PicoDI | PicoCfg | PicoLog | 原因 |
|---|---|---|---|---|
| **CLI 工具** | 選擇性 | 必要 | 必要 | 解析參數/設定、控制輸出詳盡程度。DI 備而不用，隨工具成長。 |
| **Serverless / Lambda** | 必要 | 必要 | 必要 | 冷啟動是瓶頸。AOT 編譯的 DI 搭配預先生成的解析路徑。 |
| **WASM / Blazor** | 必要 | 必要 | 必要 | 下載大小至關重要。11 個套件、對修剪友善、無執行期膨脹。 |
| **嵌入式 / IoT** | 視需要 | 必要 | 必要 | 資源受限裝置。最小二進位檔，盡可能零配置。 |

---

## 套件列表

| 套件 | 說明 | NuGet |
|---|---|---|
| **PicoDI** | 零反射 DI 容器，搭配編譯期原始碼生成 | [![NuGet](https://img.shields.io/nuget/v/PicoDI)](https://nuget.org/packages/PicoDI) |
| **PicoDI.Abs** | PicoDI 的抽象層（`ISvcContainer`、`ISvcScope`、`SvcDescriptor`） | [![NuGet](https://img.shields.io/nuget/v/PicoDI.Abs)](https://nuget.org/packages/PicoDI.Abs) |
| **PicoDI.Gen** | Roslyn source generator &mdash; 編譯期註冊與解析 | [![NuGet](https://img.shields.io/nuget/v/PicoDI.Gen)](https://nuget.org/packages/PicoDI.Gen) |
| **PicoCfg** | 非同步優先的設定管理，採用提供者模型 | [![NuGet](https://img.shields.io/nuget/v/PicoCfg)](https://nuget.org/packages/PicoCfg) |
| **PicoCfg.Abs** | PicoCfg 的抽象層（`ICfg`、`ICfgRoot`、`ICfgSection`） | [![NuGet](https://img.shields.io/nuget/v/PicoCfg.Abs)](https://nuget.org/packages/PicoCfg.Abs) |
| **PicoCfg.Gen** | 型別化設定綁定的 source generator（`CfgBind.Bind<T>`） | [![NuGet](https://img.shields.io/nuget/v/PicoCfg.Gen)](https://nuget.org/packages/PicoCfg.Gen) |
| **PicoCfg.DI** | PicoCfg 的 PicoDI 整合（`RegisterCfgRoot`、`RegisterCfgSingleton`） | [![NuGet](https://img.shields.io/nuget/v/PicoCfg.DI)](https://nuget.org/packages/PicoCfg.DI) |
| **PicoLog** | 結構化日誌，搭配編譯期訊息範本 | [![NuGet](https://img.shields.io/nuget/v/PicoLog)](https://nuget.org/packages/PicoLog) |
| **PicoLog.Abs** | PicoLog 的抽象層（`ILogger`、`ILogSink`、`LogLevel`） | [![NuGet](https://img.shields.io/nuget/v/PicoLog.Abs)](https://nuget.org/packages/PicoLog.Abs) |
| **PicoLog.Gen** | `[PicoLogMessage]` 方法的 source generator | [![NuGet](https://img.shields.io/nuget/v/PicoLog.Gen)](https://nuget.org/packages/PicoLog.Gen) |
| **PicoLog.DI** | PicoLog 的 PicoDI 整合（`AddPicoLog`、`WriteTo`、`ReadFrom`） | [![NuGet](https://img.shields.io/nuget/v/PicoLog.DI)](https://nuget.org/packages/PicoLog.DI) |

---

## 比較

### vs Autofac

Autofac 是一個成熟且功能豐富的 DI 容器，具備屬性注入、裝飾器、攔截器和模組系統。PicoDI 採取完全相反的路線：**零執行期反射、僅編譯期註冊、天生 AOT 安全**。如果你需要執行期的靈活性，請用 Autofac。如果你需要 **AOT 和最小開銷**，請用 PicoDI。

### vs Lamar

Lamar 是一個高效能的 DI 容器，使用執行期程式碼生成（`DynamicAssembly` + IL emit）。PicoDI 使用 Roslyn source generators 進行**編譯期**程式碼生成。Lamar 支援更多功能（攔截、裝飾）；PicoDI 則追求極簡與 AOT 優先。

### vs Serilog

Serilog 是 .NET 結構化日誌的黃金標準，擁有龐大的接收器生態系。PicoLog 不是 Serilog 的替代品 &mdash; 它是一個**輕量級替代方案**，適合那些重視 AOT 相容性和最小依賴、而非接收器多樣性的專案。PicoLog 的訊息範本 source generator 提供了可比的結構化日誌品質。

### vs Microsoft.Extensions

PicoHex 不是 `Microsoft.Extensions` 的擴充 &mdash; 它是一個**替代方案**。從底層即為 Native AOT 設計，採用編譯期程式碼生成而非執行期反射。如果你在開發傳統的 ASP.NET 應用程式，請繼續使用 Microsoft.Extensions。如果你在開發 **CLI 工具、Serverless 函數、WASM 應用程式或嵌入式系統**，PicoHex 正是為你的使用場景而生。

---

## 授權條款與貢獻

MIT License。原始碼倉庫：[https://github.com/PicoHex/PicoHex](https://github.com/PicoHex/PicoHex)
