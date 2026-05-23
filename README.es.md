# PicoHex

**Infraestructura mínima universal para .NET, priorizando Native AOT**

Infraestructura universal mínima para aplicaciones .NET de nivel de producción &mdash; tres módulos, once paquetes, cero reflexión en tiempo de ejecución.

[![NuGet](https://img.shields.io/nuget/v/PicoDI)](https://nuget.org/packages/PicoDI)
[![License: MIT](https://img.shields.io/badge/License-MIT-green.svg)](LICENSE)
[![CI](https://github.com/PicoHex/PicoHex/actions/workflows/ci.yml/badge.svg)](https://github.com/PicoHex/PicoHex/actions/workflows/ci.yml)

[English](README.md) | [简体中文](README.zh.md) | [日本語](README.ja.md) | [Español](README.es.md) | [Português](README.pt.md) | [繁體中文](README.zh-tw.md) | [한국어](README.ko.md) | [Français](README.fr.md) | [Deutsch](README.de.md) | [Русский](README.ru.md)

---

## Modelo Computacional

```
Configuration  ──→  Dependency Injection  ──→  Logging
  (Input)              (Core)                   (Output)
```

Toda aplicación lee configuración, ensambla sus componentes internos y produce resultados. **PicoHex** es la implementación mínima de estas tres meta-operaciones para .NET Native AOT, diseñada desde cero para generar código en tiempo de compilación en lugar de usar reflexión en tiempo de ejecución.

---

## ¿Por qué PicoHex?

| | Microsoft.Extensions | PicoHex |
|---|---|---|
| **Paquetes** | 40+ | 11 |
| **Reflexión en ejecución** | Intensiva (`Activator.CreateInstance`, árboles Expression) | Cero (generadores de código fuente) |
| **Compatibilidad AOT** | Requiere configuración cuidadosa | AOT First: compila a nativo sin ajustes |
| **¿Requiere HostBuilder?** | Sí, necesario para integrar DI + Config + Logging | No, basta con `new SvcContainer()` |
| **Integración de módulos** | En registro en ejecución (`IServiceCollection`) | En compilación mediante `ModuleInitializer` + generadores |
| **Arranque en frío** | Lento (JIT + reflexión) | Rápido (rutas pregeneradas) |
| **Tamaño del binario** | Grande (arrastra muchos ensamblados) | Mínimo (recortable, optimizado para el enlazador) |

---

## Inicio Rápido

### Solo Configuración

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

### Solo DI

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
PicoDI también soporta **AOP/interceptores en tiempo de compilación** — encadena .InterceptBy<TInterceptor>() después de Register() y el generador de código fuente emite clases decoradoras en tiempo de compilación. [Más información →](PicoDI/README.md#interceptor--aop-compile-time-decorators)
```

### Solo Registro (Logging)

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

### Todo Junto

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

## Paquetes

| Paquete | Descripción |
|---|---|
| **PicoDI** | Contenedor DI sin reflexión |
| **PicoDI.Abs** | Abstracciones para DI |
| **PicoDI.Gen** | Generador de registro en compilación |
| **PicoCfg** | Configuración asíncrona |
| **PicoCfg.Abs** | Abstracciones para configuración |
| **PicoCfg.Gen** | Generador de enlace tipado |
| **PicoCfg.DI** | Integración DI para PicoCfg |
| **PicoLog** | Registro estructurado |
| **PicoLog.Abs** | Abstracciones para registro |
| **PicoLog.Gen** | Generador `[PicoLogMessage]` |
| **PicoLog.DI** | Integración DI para PicoLog |

---

## Filosofía de Diseño

**克制 (Moderación)** — Solo DI, Config y Logging. Sin frameworks web, sin ORM, sin colas de mensajes. Si no es infraestructura universal, no pertenece aquí. PicoHex es el denominador común que toda aplicación necesita — ni más, ni menos.

**专注 (Enfoque)** — Cada módulo hace una sola cosa. PicoDI es un contenedor, no un localizador de servicios. PicoCfg es configuración, no un sistema de banderas de funcionalidad. PicoLog es registro, no una tubería de telemetría. Especialización profunda sobre generalidad superficial.

**优雅 (Elegancia)** — Las APIs son mínimas. Los generadores de código realizan las conexiones en tiempo de compilación. El desarrollador escribe código directo; las herramientas manejan la complejidad. `new SvcContainer()` reemplaza más de 100 líneas de ceremonia con `Host.CreateDefaultBuilder()`.

**高效 (Eficiencia)** — AOT First no es una ocurrencia tardía, es la base. Cero reflexión. Cero sobrecarga en ejecución. Todo lo que puede resolverse en compilación se resuelve en compilación. Binarios mínimos, arranques en frío rápidos, rendimiento predecible.

---

## Más Información

- [PicoDI](PicoDI/README.md) — Contenedor DI, registro, generador de código
- [PicoCfg](PicoCfg/README.md) — Proveedores de configuración, enlace, supervisión de archivos
- [PicoLog](PicoLog/README.md) — Registro estructurado, sumideros, plantillas de mensaje
- [Contribuir](CONTRIBUTING.md)
- [Seguridad](SECURITY.md)

---

Licencia MIT. [https://github.com/PicoHex/PicoHex](https://github.com/PicoHex/PicoHex)
