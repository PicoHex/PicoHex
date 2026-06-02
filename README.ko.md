# PicoHex

**AOT First .NET 범용 최소 인프라**

다섯 개의 모듈, 18개의 패키지, 런타임 리플렉션 제로.

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
| **콜드 스타트** | 빠름 (JIT) | 빠름 (사전 생성된 코드 경로) |
| **바이너리 크기** | 큼 | 최소 (트리밍 가능, 링커 친화적) |

---

## 빠른 시작 (Quick Start)

### 설정만 필요할 때

```shell
dotnet add package PicoCfg
```

```csharp
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
var container = new SvcContainer();
container.RegisterSingleton<IService>(scope => new MyService());
container.Build();
await using var scope = container.CreateScope();
var svc = scope.GetService<IService>();
```

### 로깅만 필요할 때

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

### 함께 사용할 때

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

PicoDI는 **컴파일 타임 AOP/인터셉터**도 지원합니다 — Register() 뒤에 .InterceptBy<TInterceptor>()를 체이닝하면 소스 생성기가 빌드 시점에 데코레이터 클래스를 생성합니다.[자세히 →](PicoDI/README.md#interceptor--aop-compile-time-decorators)

---

## 패키지 (Packages)

| 패키지 | 설명 |
|---|---|
| **PicoDI** | 리플렉션 없는 DI 컨테이너 |
| **PicoDI.Abs** | DI 추상화 |
| **PicoDI.Gen** | 컴파일 타임 등록 소스 생성기 |
| **PicoCfg** | 비동기 우선 설정 관리 |
| **PicoCfg.Abs** | 설정 추상화 |
| **PicoCfg.Gen** | 타입 바인딩 소스 생성기 |
| **PicoCfg.DI** | PicoCfg용 DI 통합 |
| **PicoLog** | 구조적 로깅 |
| **PicoLog.Abs** | 로깅 추상화 |
| **PicoLog.Gen** | `[PicoLogMessage]` 소스 생성기 |
| **PicoLog.DI** | PicoLog용 DI 통합 |

---

## 디자인 철학 (Design Philosophy)

**克制 (Restraint)** &mdash; 오직 DI, Config, Logging만. 웹 프레임워크도, ORM도, 메시지 큐도 없습니다. 범용 인프라가 아니라면 포함되지 않습니다. PicoHex는 모든 애플리케이션이 필요로 하는 공통 분모입니다 &mdash; 그 이상도 이하도 아닙니다.

**专注 (Focus)** &mdash; 각 모듈은 한 가지 일만 합니다. PicoDI는 컨테이너이지 서비스 로케이터가 아닙니다. PicoCfg는 설정 관리이지 기능 플래그 시스템이 아닙니다. PicoLog는 로깅이지 텔레메트리 파이프라인이 아닙니다. 얕은 일반성보다 깊은 전문화.

**优雅 (Elegance)** &mdash; `new SvcContainer()` 한 줄이 `Host.CreateDefaultBuilder()`의 100줄이 넘는 상용구를 대체합니다. 소스 생성기가 컴파일 타임에 연결합니다.

**高效 (Efficiency)** &mdash; AOT First. 리플렉션 제로. 컴파일 타임에 해결할 수 있는 모든 것은 컴파일 타임에 해결합니다.

---

## 더 알아보기 (Learn More)

- [PicoDI](PicoDI/README.md) &mdash; DI 컨테이너, 등록, 소스 생성기
- [PicoCfg](PicoCfg/README.md) &mdash; 설정 프로바이더, 바인딩, 파일 감시
- [PicoLog](PicoLog/README.md) &mdash; 구조적 로깅, 싱크, 메시지 템플릿
- [기여하기 (Contributing)](CONTRIBUTING.md)
- [보안 (Security)](SECURITY.md)

---

MIT License. [https://github.com/PicoHex/PicoHex](https://github.com/PicoHex/PicoHex)