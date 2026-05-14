# PicoHex

**AOT First 的 .NET 通用最小基础设施**

生产级 .NET 应用程序的最小通用基础设施——三个模块、十一个包、零运行时反射。

[![NuGet](https://img.shields.io/nuget/v/PicoDI)](https://nuget.org/packages/PicoDI)
[![License: MIT](https://img.shields.io/badge/License-MIT-green.svg)](LICENSE)
[![CI](https://github.com/PicoHex/PicoHex/actions/workflows/ci.yml/badge.svg)](https://github.com/PicoHex/PicoHex/actions/workflows/ci.yml)



[English](README.md) | [简体中文](README.zh.md) | [日本語](README.ja.md) | [Español](README.es.md) | [Português](README.pt.md) | [繁體中文](README.zh-tw.md) | [한국어](README.ko.md) | [Français](README.fr.md) | [Deutsch](README.de.md) | [Русский](README.ru.md)

---

## 计算模型

```
配置  ──→  依赖注入  ──→  日志
(输入)      (核心)        (输出)
```

每个应用程序都要读取配置、组装内部结构并产生输出。**PicoHex** 是这三个元操作在 .NET Native AOT 下的最小化实现——从头开始设计，基于编译时代码生成而非运行时反射。

---

## 为什么选择 PicoHex

| | Microsoft.Extensions | PicoHex |
|---|---|---|
| **包数量** | 40+ | 11 |
| **运行时反射** | 大量 (`Activator.CreateInstance`、表达式树) | 零 (源代码生成器) |
| **AOT 就绪** | 需要主动启用和精细配置 | AOT First——开箱即用地原生编译 |
| **需要 HostBuilder** | 是——必须用它串联 DI + Config + Logging | 否——`new SvcContainer()` 即可 |
| **模块集成** | 运行时注册 (`IServiceCollection`) | 编译时通过 `ModuleInitializer` + 源代码生成器 |
| **冷启动** | 慢 (JIT + 反射) | 快 (预生成的代码路径) |
| **二进制体积** | 大 (引入大量程序集) | 极小 (可裁剪，链接器友好) |

---

## 性能

基准测试运行于 **.NET 10.0.5、Windows 10、X64、Native AOT、Release 模式**。

### PicoDI vs Microsoft.Extensions.DependencyInjection

**20/20 全胜，平均快 2.83&times;，最高快 4.00&times; (DeepChain &times; Transient)。**

| 场景 | PicoDI (ns) | MsDI (ns) | 加速比 |
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

**混合工作负载下快 1.35&times;&ndash;1.57&times;。**

| 场景 | PicoCfg (ns) | MsConfig (ns) | 加速比 |
|---|---|---|---|
| Mixed n=100, p=2, l=1 | 5,920.9 | 9,273.9 | **1.57&times;** |
| Mixed n=100, p=2, l=10 | 29,831.8 | 40,218.9 | **1.35&times;** |

### PicoLog vs Microsoft.Extensions.Logging

PicoLog 构建了更丰富的日志条目（时间戳、类别、作用域、属性），因此异步交接路径与 Microsoft 轻量级字符串通道交接处于可比水平。**对照基准测试**衡量了去除队列/接收器开销后的底层效率：

| 对照基准 | N=1 | N=10 | N=100 |
|---|---|---|---|
| **TimestampNowOnly** | 4.15&times; | 5.05&times; | 4.54&times; |
| **LogEntryAllocateOnly** | 8.48&times; | 23.48&times; | 20.11&times; |

---

## 快速开始

### 仅使用配置

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

### 仅使用 DI

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

### 仅使用日志

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

零反射的 DI 容器，基于编译时源代码生成。

### 注册

所有 `Register*` 方法均返回 `ISvcContainer`，支持流式链式调用。

```csharp
var container = new SvcContainer();

// 基于工厂（始终可用，无需源代码生成）
container.RegisterSingleton<IService>(scope => new Service(scope.GetService<IDep>()));
container.RegisterScoped<IRepository>(scope => new Repository());
container.RegisterTransient<IValidator>(scope => new Validator());

// 预构建实例
container.RegisterSingle<IClock>(SystemClock.Instance);

// 开放泛型
container.Register(typeof(IRepository<>), typeof(SqlRepository<>), SvcLifetime.Scoped);

// 基于类型（需要 PicoDI.Gen 源代码生成器）
container.RegisterSingleton<IService, Service>();

// 托管服务
container.RegisterHostedSvc<BackgroundWorker>();

container.Build();  // 冻结注册
```

### 解析

```csharp
using var scope = container.CreateScope();

// 类型化解析（通过生成的 Resolve.* 方法实现零查找）
var svc = scope.GetService<IService>();
var repos = scope.GetServices<IRepository>();

// 基于类型的解析
var instance = scope.GetService(typeof(IService));
```

### 生命周期

| 生命周期 | 实例化 | 释放 |
|---|---|---|
| **Transient** | 每次请求新建 | 由解析所在作用域跟踪，按 LIFO 顺序释放 |
| **Scoped** | 每个作用域一次 | 作用域释放时释放 |
| **Singleton** | 每个容器一次 | 容器释放时释放 |

### 源代码生成器 (PicoDI.Gen)

添加 `PicoDI.Gen` 作为分析器，启用编译时基于类型的注册：

```xml
<PackageReference Include="PicoDI.Gen" PrivateAssets="all" />
```

生成器会扫描所有 `Register*` 调用，并生成以下内容：

- **`ConfigureGeneratedServices()`** 扩展方法，内含内联工厂委托（无反射）
- **类型化的 `Resolve.*`** 方法，实现零查找解析路径
- **编译时循环依赖检测**
- **开放泛型元数据**，支持跨程序集发现
- **`[ModuleInitializer]`** 自动配置器，注册到 `SvcContainerAutoConfiguration`

**之前**（你的代码）：
```csharp
var container = new SvcContainer();
container.RegisterSingleton<IService, Service>();
container.Build();
```

**之后**（生成的代码）：
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

可选的流式构建器，用于托管服务：

```csharp
using var hostBuilder = new SvcHostBuilder();
var host = await hostBuilder
    .ConfigureServices(container =>
    {
        container.RegisterSingleton<IService>(_ => new Service());
        container.RegisterHostedSvc<Worker>();
    })
    .BuildAsync();

// 应用程序将一直运行，直到被停止
await host.StopAsync();
```

---

## PicoCfg

异步优先的配置管理，基于提供者模型。

### 提供者模型

```
数据源 ──→ 提供者 ──→ 根节点 ──→ 消费者
```

数据源定义了配置**如何**产生。提供者是其实例化后的对象。根节点将各提供者的快照组合为统一视图。消费者通过 `TryGetValue` 或 `GetValue` 进行查询。

### 内置数据源

| 数据源 | 描述 |
|---|---|
| **Dictionary** | 内存中的键值对 |
| **Environment Variables** | 操作系统环境变量，支持前缀过滤，`__` &rarr; `:` 映射 |
| **Command Line** | `--key=value`、`--key value`、`-key value`、`/key value` |
| **Stream** | 基于行的 `key=value` 文本解析，支持文件监控 |
| **File Watching** | 文件变更时自动重新加载，带防抖处理 |
| **Chained** | 回退到另一个 `ICfg` 实例 |
| **KeyPerFile** | Kubernetes ConfigMap 风格——文件名=键，内容=值 |

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

### 源代码生成的绑定

添加 `PicoCfg.Gen` 作为分析器。源代码生成器在编译时从 `CfgBind.Bind<T>` / `CfgBind.TryBind<T>` / `CfgBind.BindInto<T>` 调用站点发现绑定目标——无需属性标记。生成的代码注册强类型绑定器，通过键路径解析配置值，无需运行时反射。

```csharp
using PicoCfg;

public sealed class AppSettings
{
    public string Name { get; init; }
    public int MaxRetries { get; init; } = 3;
    public bool EnableFeature { get; init; }
}

// 调用站点触发 AppSettings 的源代码生成
var settings = CfgBind.Bind<AppSettings>(cfg, "App");
```

---

## PicoLog

结构化日志，基于编译时消息模板。

### 日志级别

| 级别 | 值 | 用途 |
|---|---|---|
| **Emergency** | 0 | 系统不可用 |
| **Alert** | 1 | 需要立即采取行动 |
| **Critical** | 2 | 严重错误条件 |
| **Error** | 3 | 错误条件 |
| **Warning** | 4 | 警告条件 |
| **Notice** | 5 | 正常但重要的事件 |
| **Info** | 6 | 常规信息消息 |
| **Debug** | 7 | 调试级别消息 |
| **Trace** | 8 | 详细的诊断跟踪 |
| **None** | 255 | 禁用所有日志 |

### 消息模板

同时支持字符串插值和带命名参数的 `FormattableString`，用于结构化日志：

```csharp
logger.Info($"Processing order {orderId} for {customer}");
logger.Log(LogLevel.Info, $"User {user} logged in from {ipAddress}");

// 带显式属性的结构化日志
logger.LogStructured(LogLevel.Error, "Payment failed",
    properties: [new("OrderId", orderId), new("Amount", amount)],
    exception: ex);
```

### 源代码生成的消息

在静态分部方法上使用 `[PicoLogMessage]` 特性，可生成强类型、AOT 兼容的扩展方法：

```csharp
public static partial class AppLogs
{
    [PicoLogMessage(LogLevel.Info, EventId = 1001, Message = "Order {OrderId} placed")]
    public static partial void OrderPlaced(this ILogger logger, string orderId);

    [PicoLogMessage(LogLevel.Error, EventId = 2001, Message = "Payment failed")]
    public static partial void PaymentFailed(this ILogger logger, string orderId, Exception ex);
}

// 使用
logger.OrderPlaced("ORD-12345");
logger.PaymentFailed("ORD-12345", ex);
```

### 内置接收器

```csharp
var sinks = new List<ILogSink>
{
    new ConsoleSink(new ConsoleFormatter()),          // 纯文本控制台
    new ColoredConsoleSink(new ConsoleFormatter()),    // 按级别颜色编码
    new FileSink(new ConsoleFormatter(),               // 批处理文件输出
        new FileSinkOptions { FilePath = "app.log", BatchSize = 100 })
};
using var factory = new LoggerFactory(sinks);
```

### 自定义接收器

```csharp
public sealed class CustomSink : ILogSink
{
    public Task WriteAsync(LogEntry entry, CancellationToken ct = default)
    {
        // 写入你的后端
        return Task.CompletedTask;
    }

    public void Dispose() { }
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
```

---

## 模块集成

当同时引用 `PicoDI`、`PicoCfg.DI` 和 `PicoLog.DI` 时，组装过程自动完成：

1. 每个模块的源代码生成器注册一个基于 `ModuleInitializer` 的配置器
2. `new SvcContainer()` 通过 `SvcContainerAutoConfiguration.TryApplyConfiguration` 运行所有配置器
3. 无需手动组装

### DI 集成 API

**PicoCfg.DI** &mdash; `ISvcContainer` 的扩展方法：
- `RegisterCfgRoot(ICfgRoot root)` &mdash; 注册 `ICfgRoot` 和 `ICfg`
- `RegisterCfgTransient/Scoped/Singleton<T>(string? section)` &mdash; 从配置绑定 POCO
- `RegisterCfgOptionsSingleton/Scoped<T>(string? section)` &mdash; 类型化选项支持

**PicoLog.DI** &mdash; `ISvcContainer` 的扩展方法：
- `AddPicoLog(Action<LoggingOptions> configure)` &mdash; 注册 `ILoggerFactory` 和 `ILogger<>`
- `WriteTo.Console()` / `.ColoredConsole()` / `.File(path)` / `.Sink(custom)` &mdash; 接收器配置
- `ReadFrom.RegisteredSinks()` &mdash; 包含 DI 注册的 `ILogSink` 实例

### 最小化组合示例

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

## 设计哲学

**克制** — 仅包含 DI、配置和日志。没有 Web 框架，没有 ORM，没有消息队列。不属于通用基础设施的，就不应该存在。PicoHex 是每个应用都需要的公共部分——不多不少。

**专注** — 每个模块只做一件事。PicoDI 是容器，不是服务定位器。PicoCfg 是配置管理，不是功能开关系统。PicoLog 是日志记录，不是遥测管道。追求深度的专门化，而非浅层的泛化。

**优雅** — API 保持最小化。源代码生成器在编译时完成组装。开发者编写直观的代码，工具处理复杂性。`new SvcContainer()` 替代了 `Host.CreateDefaultBuilder()` 那 100 多行的仪式性代码。

**高效** — AOT First 并非事后补丁，而是基础设施。零反射，零运行时开销。一切能在编译时完成的工作，都在编译时完成。极小的二进制体积、快速的冷启动、可预测的性能。

---

## 适用场景

| 场景 | PicoDI | PicoCfg | PicoLog | 原因 |
|---|---|---|---|---|
| **CLI 工具** | 可选 | 必需 | 必需 | 解析参数/配置，控制输出详细程度。工具需要扩展时 DI 随时可用。 |
| **Serverless / Lambda** | 必需 | 必需 | 必需 | 冷启动是瓶颈。AOT 编译的 DI 配合预生成的解析路径。 |
| **WASM / Blazor** | 必需 | 必需 | 必需 | 下载体积至关重要。11 个包，裁剪友好，无运行时膨胀。 |
| **嵌入式 / IoT** | 按需 | 必需 | 必需 | 资源受限设备。极小二进制，尽可能零分配。 |

---

## 包列表

| 包名 | 描述 | NuGet |
|---|---|---|
| **PicoDI** | 零反射的 DI 容器，基于编译时源代码生成 | [![NuGet](https://img.shields.io/nuget/v/PicoDI)](https://nuget.org/packages/PicoDI) |
| **PicoDI.Abs** | PicoDI 的抽象层 (`ISvcContainer`、`ISvcScope`、`SvcDescriptor`) | [![NuGet](https://img.shields.io/nuget/v/PicoDI.Abs)](https://nuget.org/packages/PicoDI.Abs) |
| **PicoDI.Gen** | Roslyn 源代码生成器——编译时注册与解析 | [![NuGet](https://img.shields.io/nuget/v/PicoDI.Gen)](https://nuget.org/packages/PicoDI.Gen) |
| **PicoCfg** | 异步优先的配置管理，基于提供者模型 | [![NuGet](https://img.shields.io/nuget/v/PicoCfg)](https://nuget.org/packages/PicoCfg) |
| **PicoCfg.Abs** | PicoCfg 的抽象层 (`ICfg`、`ICfgRoot`、`ICfgSection`) | [![NuGet](https://img.shields.io/nuget/v/PicoCfg.Abs)](https://nuget.org/packages/PicoCfg.Abs) |
| **PicoCfg.Gen** | 类型化配置绑定的源代码生成器 (`CfgBind.Bind<T>`) | [![NuGet](https://img.shields.io/nuget/v/PicoCfg.Gen)](https://nuget.org/packages/PicoCfg.Gen) |
| **PicoCfg.DI** | PicoCfg 的 PicoDI 集成 (`RegisterCfgRoot`、`RegisterCfgSingleton`) | [![NuGet](https://img.shields.io/nuget/v/PicoCfg.DI)](https://nuget.org/packages/PicoCfg.DI) |
| **PicoLog** | 结构化日志，基于编译时消息模板 | [![NuGet](https://img.shields.io/nuget/v/PicoLog)](https://nuget.org/packages/PicoLog) |
| **PicoLog.Abs** | PicoLog 的抽象层 (`ILogger`、`ILogSink`、`LogLevel`) | [![NuGet](https://img.shields.io/nuget/v/PicoLog.Abs)](https://nuget.org/packages/PicoLog.Abs) |
| **PicoLog.Gen** | `[PicoLogMessage]` 方法的源代码生成器 | [![NuGet](https://img.shields.io/nuget/v/PicoLog.Gen)](https://nuget.org/packages/PicoLog.Gen) |
| **PicoLog.DI** | PicoLog 的 PicoDI 集成 (`AddPicoLog`、`WriteTo`、`ReadFrom`) | [![NuGet](https://img.shields.io/nuget/v/PicoLog.DI)](https://nuget.org/packages/PicoLog.DI) |

---

## 对比

### vs Autofac

Autofac 是一个成熟、功能丰富的 DI 容器，支持属性注入、装饰器、拦截器和模块。PicoDI 采取了相反的方向：**零运行时反射、仅编译时注册、天生 AOT 安全**。如果你需要运行时灵活性，请使用 Autofac。如果你需要 **AOT 和最小开销**，请使用 PicoDI。

### vs Lamar

Lamar 是一个高性能的 DI 容器，使用运行时代码生成（`DynamicAssembly` + IL 发射）。PicoDI 使用 Roslyn 源代码生成器进行**编译时**代码生成。Lamar 支持更多功能（拦截、装饰）；PicoDI 追求最小化和 AOT 优先。

### vs Serilog

Serilog 是 .NET 结构化日志的事实标准，拥有庞大的接收器生态。PicoLog 并非 Serilog 的替代品——它是一个**轻量级替代方案**，适用于重视 AOT 兼容性和最小依赖而非接收器多样性的项目。PicoLog 的消息模板源代码生成器提供了同等水平的结构化日志质量。

### vs Microsoft.Extensions

PicoHex 不是 `Microsoft.Extensions` 的扩展——它是一个**替代方案**。从底层为 Native AOT 设计，使用编译时代码生成而非运行时反射。如果你在构建传统的 ASP.NET 应用程序，请继续使用 Microsoft.Extensions。如果你在构建 **CLI 工具、无服务器函数、WASM 应用或嵌入式系统**，PicoHex 正是为你而生。

---

## 许可证与贡献

MIT 许可证。仓库地址：[https://github.com/PicoHex/PicoHex](https://github.com/PicoHex/PicoHex)
