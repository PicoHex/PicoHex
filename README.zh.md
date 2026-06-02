# PicoHex

**AOT-First 通用最小基础设施 for .NET**

五个模块，零运行时反射。

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

await using var scope = container.CreateScope();
var svc = scope.GetService<IService>();
```

PicoDI 也支持**编译期 AOP/拦截器**——在 `Register()` 后链式调用 `.InterceptBy<TInterceptor>()`，源代码生成器会在编译时生成装饰器类。[了解更多 →](PicoDI/README.md#interceptor--aop-compile-time-decorators)

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

### 完整集成

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
await using var scope = container.CreateScope();
var logger = scope.GetService<ILogger<Program>>();
logger.Info("App started");
```

---

## 包一览

| 包 | 描述 |
|---|---|
| **PicoDI** | 零反射 DI 容器 |
| **PicoDI.Abs** | DI 抽象层 |
| **PicoDI.Gen** | 编译时注册源生成器 |
| **PicoCfg** | 异步优先配置 |
| **PicoCfg.Abs** | 配置抽象层 |
| **PicoCfg.Gen** | 类型绑定源生成器 |
| **PicoCfg.DI** | PicoCfg 的 DI 集成 |
| **PicoLog** | 结构化日志 |
| **PicoLog.Abs** | 日志抽象层 |
| **PicoLog.Gen** | `[PicoLogMessage]` 源生成器 |
| **PicoLog.DI** | PicoLog 的 DI 集成 |

---

## 设计哲学

**克制** — 仅包含 DI、配置和日志。没有 Web 框架，没有 ORM，没有消息队列。不属于通用基础设施的，就不应该存在。PicoHex 是每个应用都需要的公共部分——不多不少。

**专注** — 每个模块只做一件事。PicoDI 是容器，不是服务定位器。PicoCfg 是配置管理，不是功能开关系统。PicoLog 是日志记录，不是遥测管道。追求深度的专门化，而非浅层的泛化。

**优雅** — API 保持最小化。源代码生成器在编译时完成组装。开发者编写直观的代码，工具处理复杂性。`new SvcContainer()` 替代了 `Host.CreateDefaultBuilder()` 那 100 多行的仪式性代码。

**高效** — AOT First 并非事后补丁，而是基础设施。零反射，零运行时开销。一切能在编译时完成的工作，都在编译时完成。极小的二进制体积、快速的冷启动、可预测的性能。

---

## 了解更多

- [PicoDI](PicoDI/README.md) — DI 容器、注册、源生成器
- [PicoCfg](PicoCfg/README.md) — 配置提供者、绑定、文件监听
- [PicoLog](PicoLog/README.md) — 结构化日志、Sink、消息模板
- [贡献指南](CONTRIBUTING.md)
- [安全](SECURITY.md)

---

MIT 许可证。[https://github.com/PicoHex/PicoHex](https://github.com/PicoHex/PicoHex)
