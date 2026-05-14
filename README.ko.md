# PicoHex

**AOT First .NET 범용 최소 인프라**

프로덕션 등급 .NET 애플리케이션을 위한 최소 범용 인프라 &mdash; 세 개의 모듈, 열한 개의 패키지, 런타임 리플렉션 제로.

[![NuGet](https://img.shields.io/nuget/v/PicoDI)](https://nuget.org/packages/PicoDI)
[![License: MIT](https://img.shields.io/badge/License-MIT-green.svg)](LICENSE)
[![CI](https://github.com/PicoHex/PicoHex/actions/workflows/ci.yml/badge.svg)](https://github.com/PicoHex/PicoHex/actions/workflows/ci.yml)



[English](README.md) | [简体中文](README.zh.md) | [日本語](README.ja.md) | [Español](README.es.md) | [Português](README.pt.md) | [繁體中文](README.zh-tw.md) | [한국어](README.ko.md) | [Français](README.fr.md) | [Deutsch](README.de.md) | [Русский](README.ru.md)

---

## 계산 모델 (Computational Model)

```
Configuration  ──→  Dependency Injection  ──→  Logging
  (입력)              (핵심)                   (출력)
```

모든 애플리케이션은 설정을 읽고, 내부를 조립하며, 출력을 생성합니다. **PicoHex**는 .NET Native AOT를 위해 이 세 가지 메타 연산을 최소한으로 구현한 것입니다 &mdash; 런타임 리플렉션 대신 컴파일 타임 코드 생성을 근간으로 설계되었습니다.

---

## 왜 PicoHex인가

| | Microsoft.Extensions | PicoHex |
|---|---|---|
| **패키지 수** | 40+ | 11 |
| **런타임 리플렉션** | 과도함 (`Activator.CreateInstance`, Expression trees) | 없음 (소스 생성기) |
| **AOT 지원** | 옵트인 및 세심한 설정 필요 | AOT First &mdash; 기본적으로 네이티브 컴파일 |
| **HostBuilder 필요** | 예 &mdash; DI + Config + Logging 연결 필수 | 아니요 &mdash; `new SvcContainer()`면 충분 |
| **모듈 통합** | 런타임 등록 (`IServiceCollection`) | 컴파일 타임 (`ModuleInitializer` + 소스 생성기) |
| **콜드 스타트** | 느림 (JIT + 리플렉션) | 빠름 (사전 생성된 코드 경로) |
| **바이너리 크기** | 큼 (많은 어셈블리 포함) | 최소 (트리밍 가능, 링커 친화적) |

---

## 성능 (Performance)

벤치마크는 **.NET 10.0.5, Windows 10, X64, Native AOT, Release 모드**에서 실행되었습니다.

### PicoDI vs Microsoft.Extensions.DependencyInjection

**20/20 승리, 평균 2.83배 빠름, 최대 4.00배 (DeepChain × Transient).**

| 시나리오 | PicoDI (ns) | MsDI (ns) | 속도 향상 |
|---|---|---|---|
| **DeepChain × Transient** | 156.7 | 626.9 | **4.00배** |
| **NoDependency × Singleton** | 14.2 | 55.1 | **3.89배** |
| **MultipleResolutions × Singleton** | 1,523.6 | 5,373.9 | **3.53배** |
| **MultipleResolutions × Transient** | 4,672.4 | 16,402.1 | **3.51배** |
| **NoDependency × Transient** | 27.7 | 97.1 | **3.50배** |
| ContainerSetup | 739.9 | 1,919.1 | 2.59배 |
| SingleResolution × Transient | 55.3 | 187.8 | 3.39배 |
| ScopeCreation | 94.1 | 104.5 | 1.11배 |

### PicoCfg vs Microsoft.Extensions.Configuration

**혼합 워크로드에서 1.35배&ndash;1.57배 더 빠름.**

| 시나리오 | PicoCfg (ns) | MsConfig (ns) | 속도 향상 |
|---|---|---|---|
| Mixed n=100, p=2, l=1 | 5,920.9 | 9,273.9 | **1.57배** |
| Mixed n=100, p=2, l=10 | 29,831.8 | 40,218.9 | **1.35배** |

### PicoLog vs Microsoft.Extensions.Logging

PicoLog은 더 풍부한 로그 항목(타임스탬프, 카테고리, 스코프, 속성)을 구성하므로, 비동기 핸드오프 경로가 Microsoft의 경량 문자열 채널 핸드오프와 비슷한 수준입니다. **제어 벤치마크**는 큐/싱크 오버헤드 없이 기본 효율성을 측정합니다:

| 제어 벤치마크 | N=1 | N=10 | N=100 |
|---|---|---|---|
| **TimestampNowOnly** | 4.15배 | 5.05배 | 4.54배 |
| **LogEntryAllocateOnly** | 8.48배 | 23.48배 | 20.11배 |

---

## 빠른 시작 (Quick Start)

### 설정만 필요할 때

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

### DI만 필요할 때

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

### 로깅만 필요할 때

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

리플렉션 없는 DI 컨테이너, 컴파일 타임 소스 생성 기반.

### 등록 (Registration)

모든 `Register*` 메서드는 유연한 체이닝을 위해 `ISvcContainer`를 반환합니다.

```csharp
var container = new SvcContainer();

// 팩토리 기반 (항상 동작, 소스 생성 불필요)
container.RegisterSingleton<IService>(scope => new Service(scope.GetService<IDep>()));
container.RegisterScoped<IRepository>(scope => new Repository());
container.RegisterTransient<IValidator>(scope => new Validator());

// 미리 생성된 인스턴스
container.RegisterSingle<IClock>(SystemClock.Instance);

// 오픈 제네릭
container.Register(typeof(IRepository<>), typeof(SqlRepository<>), SvcLifetime.Scoped);

// 타입 기반 (PicoDI.Gen 소스 생성기 필요)
container.RegisterSingleton<IService, Service>();

// 호스티드 서비스
container.RegisterHostedSvc<BackgroundWorker>();

container.Build();  // 등록 고정
```

### 해결 (Resolution)

```csharp
using var scope = container.CreateScope();

// 타입 해결 (생성된 Resolve.* 메서드를 통한 제로-조회)
var svc = scope.GetService<IService>();
var repos = scope.GetServices<IRepository>();

// 타입 기반 해결
var instance = scope.GetService(typeof(IService));
```

### 수명 (Lifetimes)

| 수명 | 인스턴스화 | 해제 |
|---|---|---|
| **Transient** | 매번 새로 생성 | 해결 스코프가 추적, LIFO 순서로 해제 |
| **Scoped** | 스코프당 한 번 | 스코프 해제 시 해제 |
| **Singleton** | 컨테이너당 한 번 | 컨테이너 해제 시 해제 |

### 소스 생성기 (PicoDI.Gen)

컴파일 타임 타입 기반 등록을 활성화하려면 `PicoDI.Gen`을 분석기로 추가하세요:

```xml
<PackageReference Include="PicoDI.Gen" PrivateAssets="all" />
```

생성기는 모든 `Register*` 호출을 스캔하여 다음을 생성합니다:

- **`ConfigureGeneratedServices()`** 확장 메서드 (인라인 팩토리 델리게이트, 리플렉션 없음)
- **타입화된 `Resolve.*`** 메서드 (제로-조회 해결 경로)
- **컴파일 타임 순환 의존성 탐지**
- **어셈블리 간 발견을 위한 오픈 제네릭 메타데이터**
- **`SvcContainerAutoConfiguration`에 등록하는 `[ModuleInitializer]` 자동 설정기**

**Before** (작성하는 코드):
```csharp
var container = new SvcContainer();
container.RegisterSingleton<IService, Service>();
container.Build();
```

**After** (생성되는 코드):
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

호스티드 서비스를 위한 선택적 유연한 빌더:

```csharp
using var hostBuilder = new SvcHostBuilder();
var host = await hostBuilder
    .ConfigureServices(container =>
    {
        container.RegisterSingleton<IService>(_ => new Service());
        container.RegisterHostedSvc<Worker>();
    })
    .BuildAsync();

// 중지될 때까지 애플리케이션 실행
await host.StopAsync();
```

---

## PicoCfg

비동기 우선 설정 관리, 프로바이더 모델 기반.

### 프로바이더 모델 (Provider Model)

```
Sources ──→ Providers ──→ Root ──→ Consumer
```

소스는 설정이 **어떻게** 생성되는지 정의합니다. 프로바이더는 구체화된 인스턴스입니다. 루트는 프로바이더 스냅샷을 통합된 뷰로 구성합니다. 소비자는 `TryGetValue`나 `GetValue`로 조회합니다.

### 내장 소스 (Built-in Sources)

| 소스 | 설명 |
|---|---|
| **Dictionary** | 인메모리 키-값 쌍 |
| **Environment Variables** | OS 환경 변수, 접두사 필터링, `__` → `:` 매핑 |
| **Command Line** | `--key=value`, `--key value`, `-key value`, `/key value` |
| **Stream** | 라인 기반 `key=value` 텍스트 파싱 (파일 감시 포함) |
| **File Watching** | 파일 변경 시 디바운스 후 자동 리로드 |
| **Chained** | 다른 `ICfg` 인스턴스로 폴백 |
| **KeyPerFile** | Kubernetes ConfigMap 방식 &mdash; 파일명=키, 내용=값 |

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

### 소스 생성 바인딩 (Source-Generated Binding)

`PicoCfg.Gen`을 분석기로 추가하세요. 소스 생성기는 `CfgBind.Bind<T>` / `CfgBind.TryBind<T>` / `CfgBind.BindInto<T>` 호출 지점에서 컴파일 타임에 바인딩 대상을 발견합니다 &mdash; 속성 불필요. 생성된 코드는 키 경로로 설정 값을 런타임 리플렉션 없이 해결하는 강력한 타입의 바인더를 등록합니다.

```csharp
using PicoCfg;

public sealed class AppSettings
{
    public string Name { get; init; }
    public int MaxRetries { get; init; } = 3;
    public bool EnableFeature { get; init; }
}

// 호출 지점이 AppSettings에 대한 소스 생성을 트리거합니다
var settings = CfgBind.Bind<AppSettings>(cfg, "App");
```

---

## PicoLog

컴파일 타임 메시지 템플릿 기반 구조적 로깅.

### 로그 레벨 (Log Levels)

| 레벨 | 값 | 용도 |
|---|---|---|
| **Emergency** | 0 | 시스템 사용 불가 |
| **Alert** | 1 | 즉시 조치 필요 |
| **Critical** | 2 | 심각한 상태 |
| **Error** | 3 | 오류 상태 |
| **Warning** | 4 | 경고 상태 |
| **Notice** | 5 | 정상적이지만 중요 |
| **Info** | 6 | 정보 메시지 |
| **Debug** | 7 | 디버그 메시지 |
| **Trace** | 8 | 상세 진단 추적 |
| **None** | 255 | 모든 로깅 비활성화 |

### 메시지 템플릿 (Message Templates)

문자열 보간과 `FormattableString`을 모두 지원하며, 구조적 로깅을 위한 명명된 매개변수를 제공합니다:

```csharp
logger.Info($"Processing order {orderId} for {customer}");
logger.Log(LogLevel.Info, $"User {user} logged in from {ipAddress}");

// 명시적 속성을 사용한 구조적 로깅
logger.LogStructured(LogLevel.Error, "Payment failed",
    properties: [new("OrderId", orderId), new("Amount", amount)],
    exception: ex);
```

### 소스 생성 메시지 (Source-Generated Messages)

정적 partial 메서드에 `[PicoLogMessage]` 속성을 추가하면 강력한 타입의 AOT 호환 확장 메서드가 생성됩니다:

```csharp
public static partial class AppLogs
{
    [PicoLogMessage(LogLevel.Info, EventId = 1001, Message = "Order {OrderId} placed")]
    public static partial void OrderPlaced(this ILogger logger, string orderId);

    [PicoLogMessage(LogLevel.Error, EventId = 2001, Message = "Payment failed")]
    public static partial void PaymentFailed(this ILogger logger, string orderId, Exception ex);
}

// 사용
logger.OrderPlaced("ORD-12345");
logger.PaymentFailed("ORD-12345", ex);
```

### 내장 싱크 (Built-in Sinks)

```csharp
var sinks = new List<ILogSink>
{
    new ConsoleSink(new ConsoleFormatter()),          // 일반 콘솔
    new ColoredConsoleSink(new ConsoleFormatter()),    // 레벨별 색상 구분
    new FileSink(new ConsoleFormatter(),               // 배치 파일 출력
        new FileSinkOptions { FilePath = "app.log", BatchSize = 100 })
};
using var factory = new LoggerFactory(sinks);
```

### 사용자 정의 싱크 (Custom Sinks)

```csharp
public sealed class CustomSink : ILogSink
{
    public Task WriteAsync(LogEntry entry, CancellationToken ct = default)
    {
        // 백엔드에 쓰기
        return Task.CompletedTask;
    }

    public void Dispose() { }
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
```

---

## 모듈 통합 (Module Integration)

`PicoDI`, `PicoCfg.DI`, `PicoLog.DI`가 모두 참조되면 연결이 자동으로 이루어집니다:

1. 각 모듈의 소스 생성기가 `ModuleInitializer` 기반 설정기를 등록합니다.
2. `new SvcContainer()`가 `SvcContainerAutoConfiguration.TryApplyConfiguration`을 통해 모든 설정기를 실행합니다.
3. 수동 연결이 필요 없습니다.

### DI 통합 API

**PicoCfg.DI** &mdash; `ISvcContainer` 확장:
- `RegisterCfgRoot(ICfgRoot root)` &mdash; `ICfgRoot`와 `ICfg` 등록
- `RegisterCfgTransient/Scoped/Singleton<T>(string? section)` &mdash; 설정에서 POCO 바인딩
- `RegisterCfgOptionsSingleton/Scoped<T>(string? section)` &mdash; 타입화된 옵션 지원

**PicoLog.DI** &mdash; `ISvcContainer` 확장:
- `AddPicoLog(Action<LoggingOptions> configure)` &mdash; `ILoggerFactory`와 `ILogger<>` 등록
- `WriteTo.Console()` / `.ColoredConsole()` / `.File(path)` / `.Sink(custom)` &mdash; 싱크 설정
- `ReadFrom.RegisteredSinks()` &mdash; DI에 등록된 `ILogSink` 인스턴스 포함

### 최소 결합 예제

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

## 디자인 철학 (Design Philosophy)

**克制 (Restraint)** &mdash; 오직 DI, Config, Logging만. 웹 프레임워크도, ORM도, 메시지 큐도 없습니다. 범용 인프라가 아니라면 포함되지 않습니다. PicoHex는 모든 애플리케이션이 필요로 하는 공통 분모입니다 &mdash; 그 이상도 이하도 아닙니다.

**专注 (Focus)** &mdash; 각 모듈은 한 가지 일만 합니다. PicoDI는 컨테이너이지 서비스 로케이터가 아닙니다. PicoCfg는 설정 관리이지 기능 플래그 시스템이 아닙니다. PicoLog는 로깅이지 텔레메트리 파이프라인이 아닙니다. 얕은 일반성보다 깊은 전문화.

**优雅 (Elegance)** &mdash; API는 최소화되었습니다. 소스 생성기가 컴파일 타임에 연결합니다. 개발자는 직관적인 코드를 작성하고, 도구가 복잡성을 처리합니다. `new SvcContainer()` 한 줄이 `Host.CreateDefaultBuilder()`의 100줄이 넘는 상용구를 대체합니다.

**高效 (Efficiency)** &mdash; AOT First는 사후 추가가 아닌 근본 설계 원칙입니다. 리플렉션 제로. 런타임 오버헤드 제로. 컴파일 타임에 해결할 수 있는 모든 것은 컴파일 타임에 해결합니다. 최소 바이너리, 빠른 콜드 스타트, 예측 가능한 성능.

---

## 사용 사례 (Use Cases)

| 시나리오 | PicoDI | PicoCfg | PicoLog | 이유 |
|---|---|---|---|---|
| **CLI 도구** | 선택 | 필수 | 필수 | 인자/설정 파싱, 출력 상세도 제어. 도구가 커지면 DI도 필요. |
| **서버리스 / Lambda** | 필수 | 필수 | 필수 | 콜드 스타트가 병목. 사전 생성된 해결 경로를 가진 AOT 컴파일 DI. |
| **WASM / Blazor** | 필수 | 필수 | 필수 | 다운로드 크기가 중요. 11개 패키지, 트림 친화적, 런타임 블로트 없음. |
| **임베디드 / IoT** | 필요 시 | 필수 | 필수 | 리소스 제약 환경. 최소 바이너리, 가능한 제로 할당. |

---

## 패키지 (Packages)

| 패키지 | 설명 | NuGet |
|---|---|---|
| **PicoDI** | 리플렉션 없는 DI 컨테이너, 컴파일 타임 소스 생성 기반 | [![NuGet](https://img.shields.io/nuget/v/PicoDI)](https://nuget.org/packages/PicoDI) |
| **PicoDI.Abs** | PicoDI 추상화 (`ISvcContainer`, `ISvcScope`, `SvcDescriptor`) | [![NuGet](https://img.shields.io/nuget/v/PicoDI.Abs)](https://nuget.org/packages/PicoDI.Abs) |
| **PicoDI.Gen** | Roslyn 소스 생성기 &mdash; 컴파일 타임 등록 및 해결 | [![NuGet](https://img.shields.io/nuget/v/PicoDI.Gen)](https://nuget.org/packages/PicoDI.Gen) |
| **PicoCfg** | 비동기 우선 설정 관리, 프로바이더 모델 기반 | [![NuGet](https://img.shields.io/nuget/v/PicoCfg)](https://nuget.org/packages/PicoCfg) |
| **PicoCfg.Abs** | PicoCfg 추상화 (`ICfg`, `ICfgRoot`, `ICfgSection`) | [![NuGet](https://img.shields.io/nuget/v/PicoCfg.Abs)](https://nuget.org/packages/PicoCfg.Abs) |
| **PicoCfg.Gen** | 타입 설정 바인딩용 소스 생성기 (`CfgBind.Bind<T>`) | [![NuGet](https://img.shields.io/nuget/v/PicoCfg.Gen)](https://nuget.org/packages/PicoCfg.Gen) |
| **PicoCfg.DI** | PicoCfg용 PicoDI 통합 (`RegisterCfgRoot`, `RegisterCfgSingleton`) | [![NuGet](https://img.shields.io/nuget/v/PicoCfg.DI)](https://nuget.org/packages/PicoCfg.DI) |
| **PicoLog** | 컴파일 타임 메시지 템플릿 기반 구조적 로깅 | [![NuGet](https://img.shields.io/nuget/v/PicoLog)](https://nuget.org/packages/PicoLog) |
| **PicoLog.Abs** | PicoLog 추상화 (`ILogger`, `ILogSink`, `LogLevel`) | [![NuGet](https://img.shields.io/nuget/v/PicoLog.Abs)](https://nuget.org/packages/PicoLog.Abs) |
| **PicoLog.Gen** | `[PicoLogMessage]` 메서드용 소스 생성기 | [![NuGet](https://img.shields.io/nuget/v/PicoLog.Gen)](https://nuget.org/packages/PicoLog.Gen) |
| **PicoLog.DI** | PicoLog용 PicoDI 통합 (`AddPicoLog`, `WriteTo`, `ReadFrom`) | [![NuGet](https://img.shields.io/nuget/v/PicoLog.DI)](https://nuget.org/packages/PicoLog.DI) |

---

## 비교 (Comparison)

### vs Autofac

Autofac은 속성 주입, 데코레이터, 인터셉터, 모듈을 갖춘 성숙하고 기능이 풍부한 DI 컨테이너입니다. PicoDI는 반대 접근법을 취합니다: **런타임 리플렉션 제로, 컴파일 타임 전용 등록, AOT 안전 설계**. 런타임 유연성이 필요하다면 Autofac을 사용하세요. **AOT와 최소 오버헤드**가 필요하다면 PicoDI를 사용하세요.

### vs Lamar

Lamar은 런타임 코드 생성(`DynamicAssembly` + IL emit)을 사용하는 고성능 DI 컨테이너입니다. PicoDI는 Roslyn 소스 생성기를 사용하여 **컴파일 타임**에 코드를 생성합니다. Lamar은 더 많은 기능(인터셉션, 데코레이션)을 지원하고, PicoDI는 최소한이며 AOT 우선입니다.

### vs Serilog

Serilog는 방대한 싱크 생태계를 갖춘 .NET 구조적 로깅의 표준입니다. PicoLog는 Serilog를 대체하는 것이 아닙니다 &mdash; **싱크 다양성보다 AOT 호환성과 최소 의존성을 중시하는 프로젝트를 위한 경량 대안**입니다. PicoLog의 메시지 템플릿용 소스 생성기는 비슷한 수준의 구조적 로깅 품질을 제공합니다.

### vs Microsoft.Extensions

PicoHex는 `Microsoft.Extensions`의 확장이 아닙니다 &mdash; **대안**입니다. Native AOT에 맞춰 처음부터 설계되었으며, 런타임 리플렉션 대신 컴파일 타임 코드 생성을 사용합니다. 전통적인 ASP.NET 애플리케이션을 구축한다면 Microsoft.Extensions를 계속 사용하세요. **CLI 도구, 서버리스 함수, WASM 앱, 임베디드 시스템**을 구축한다면 PicoHex가 당신의 사용 사례에 맞춰 제작되었습니다.

---

## 라이선스 및 기여 (License &amp; Contributing)

MIT License. 저장소: [https://github.com/PicoHex/PicoHex](https://github.com/PicoHex/PicoHex)
