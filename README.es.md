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

## Rendimiento

Pruebas de rendimiento ejecutadas en **.NET 10.0.5, Windows 10, X64, Native AOT, modo Release**.

### PicoDI frente a Microsoft.Extensions.DependencyInjection

**20/20 victorias, 2,83&times; más rápido en promedio, máximo 4,00&times; (DeepChain &times; Transient).**

| Escenario | PicoDI (ns) | MsDI (ns) | Aceleración |
|---|---|---|---|
| **DeepChain &times; Transient** | 156,7 | 626,9 | **4,00&times;** |
| **NoDependency &times; Singleton** | 14,2 | 55,1 | **3,89&times;** |
| **MultipleResolutions &times; Singleton** | 1.523,6 | 5.373,9 | **3,53&times;** |
| **MultipleResolutions &times; Transient** | 4.672,4 | 16.402,1 | **3,51&times;** |
| **NoDependency &times; Transient** | 27,7 | 97,1 | **3,50&times;** |
| ContainerSetup | 739,9 | 1.919,1 | 2,59&times; |
| SingleResolution &times; Transient | 55,3 | 187,8 | 3,39&times; |
| ScopeCreation | 94,1 | 104,5 | 1,11&times; |

### PicoCfg frente a Microsoft.Extensions.Configuration

**Entre 1,35&times; y 1,57&times; más rápido en cargas de trabajo mixtas.**

| Escenario | PicoCfg (ns) | MsConfig (ns) | Aceleración |
|---|---|---|---|
| Mixto n=100, p=2, l=1 | 5.920,9 | 9.273,9 | **1,57&times;** |
| Mixto n=100, p=2, l=10 | 29.831,8 | 40.218,9 | **1,35&times;** |

### PicoLog frente a Microsoft.Extensions.Logging

PicoLog construye entradas de registro más ricas (marca de tiempo, categoría, ámbitos, propiedades), por lo que la ruta de entrega asíncrona es comparable a la transferencia ligera de cadenas de Microsoft. Los **puntos de control** miden la eficiencia subyacente sin la sobrecarga de la cola o el sumidero:

| Punto de control | N=1 | N=10 | N=100 |
|---|---|---|---|
| **TimestampNowOnly** | 4,15&times; | 5,05&times; | 4,54&times; |
| **LogEntryAllocateOnly** | 8,48&times; | 23,48&times; | 20,11&times; |

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

var svc = container.CreateScope().GetService<IService>();
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

---

## PicoDI

Contenedor DI sin reflexión con generación de código en tiempo de compilación.

### Registro

Todos los métodos `Register*` devuelven `ISvcContainer` para encadenamiento fluido.

```csharp
var container = new SvcContainer();

// Basado en fábrica (siempre funciona, no requiere generador)
container.RegisterSingleton<IService>(scope => new Service(scope.GetService<IDep>()));
container.RegisterScoped<IRepository>(scope => new Repository());
container.RegisterTransient<IValidator>(scope => new Validator());

// Instancia preconstruida
container.RegisterSingle<IClock>(SystemClock.Instance);

// Genéricos abiertos
container.Register(typeof(IRepository<>), typeof(SqlRepository<>), SvcLifetime.Scoped);

// Basado en tipos (requiere el generador PicoDI.Gen)
container.RegisterSingleton<IService, Service>();

// Servicios hospedados
container.RegisterHostedSvc<BackgroundWorker>();

container.Build();  // Congela los registros
```

### Resolución

```csharp
using var scope = container.CreateScope();

// Resolución con tipos concretos (búsqueda cero mediante métodos Resolve.* generados)
var svc = scope.GetService<IService>();
var repos = scope.GetServices<IRepository>();

// Resolución basada en tipos
var instance = scope.GetService(typeof(IService));
```

### Ciclos de Vida

| Ciclo de vida | Instanciación | Liberación |
|---|---|---|
| **Transient** | Nueva cada vez | Gestionada por el ámbito de resolución, liberada en orden LIFO |
| **Scoped** | Una vez por ámbito | Liberada al desechar el ámbito |
| **Singleton** | Una vez por contenedor | Liberada al desechar el contenedor |

### Generador de Código Fuente (PicoDI.Gen)

Agregue `PicoDI.Gen` como analizador para habilitar registros basados en tipos en tiempo de compilación:

```xml
<PackageReference Include="PicoDI.Gen" PrivateAssets="all" />
```

El generador examina todas las llamadas `Register*` y emite:

- **Método de extensión `ConfigureGeneratedServices()`** con delegados de fábrica en línea (sin reflexión)
- **Métodos `Resolve.*`** tipados para rutas de resolución sin búsqueda
- **Detección de dependencias circulares en compilación**
- **Metadatos de genéricos abiertos** para descubrimiento entre ensamblados
- **Auto-configurador `[ModuleInitializer]`** que se registra con `SvcContainerAutoConfiguration`

**Antes** (su código):
```csharp
var container = new SvcContainer();
container.RegisterSingleton<IService, Service>();
container.Build();
```

**Después** (generado):
```csharp
// Generado automáticamente por PicoDI.Gen
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

Constructor fluido opcional para servicios hospedados:

```csharp
using var hostBuilder = new SvcHostBuilder();
var host = await hostBuilder
    .ConfigureServices(container =>
    {
        container.RegisterSingleton<IService>(_ => new Service());
        container.RegisterHostedSvc<Worker>();
    })
    .BuildAsync();

// La aplicación se ejecuta hasta que se detiene
await host.StopAsync();
```

---

## PicoCfg

Gestión de configuración asíncrona con modelo de proveedores.

### Modelo de Proveedores

```
Sources ──→ Providers ──→ Root ──→ Consumer
```

Las fuentes definen **cómo** se produce la configuración. Los proveedores son las instancias materializadas. La raíz compone las instantáneas de los proveedores en una vista unificada. Los consumidores consultan mediante `TryGetValue` o `GetValue`.

### Fuentes Incluidas

| Fuente | Descripción |
|---|---|
| **Dictionary** | Pares clave-valor en memoria |
| **Environment Variables** | Variables de entorno del sistema, filtrado por prefijo, mapeo `__` &rarr; `:` |
| **Command Line** | `--key=value`, `--key value`, `-key value`, `/key value` |
| **Stream** | Análisis de texto `key=value` por líneas con supervisión de archivos |
| **File Watching** | Recarga automática al cambiar el archivo con debounce |
| **Chained** | Repliegue a otra instancia de `ICfg` |
| **KeyPerFile** | Estilo Kubernetes ConfigMap: nombre de archivo = clave, contenido = valor |

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

### Enlace Generado por el Generador de Código

Agregue `PicoCfg.Gen` como analizador. El generador de código descubre los destinos de enlace en tiempo de compilación a partir de los sitios de llamada `CfgBind.Bind<T>` / `CfgBind.TryBind<T>` / `CfgBind.BindInto<T>` — no se requieren atributos. El código generado registra enlazadores fuertemente tipados que resuelven valores de configuración mediante rutas de clave sin reflexión en ejecución.

```csharp
using PicoCfg;

public sealed class AppSettings
{
    public string Name { get; init; }
    public int MaxRetries { get; init; } = 3;
    public bool EnableFeature { get; init; }
}

// El sitio de llamada activa la generación de código para AppSettings
var settings = CfgBind.Bind<AppSettings>(cfg, "App");
```

---

## PicoLog

Registro estructurado con plantillas de mensaje en tiempo de compilación.

### Niveles de Registro

| Nivel | Valor | Uso |
|---|---|---|
| **Emergency** | 0 | El sistema es inutilizable |
| **Alert** | 1 | Debe tomarse acción inmediatamente |
| **Critical** | 2 | Condiciones críticas |
| **Error** | 3 | Condiciones de error |
| **Warning** | 4 | Condiciones de advertencia |
| **Notice** | 5 | Normal pero significativo |
| **Info** | 6 | Mensajes informativos |
| **Debug** | 7 | Mensajes de depuración |
| **Trace** | 8 | Trazado de diagnóstico detallado |
| **None** | 255 | Desactiva todo el registro |

### Plantillas de Mensaje

Soporte tanto para interpolación de cadenas como para `FormattableString` con parámetros nombrados para registro estructurado:

```csharp
logger.Info($"Processing order {orderId} for {customer}");
logger.Log(LogLevel.Info, $"User {user} logged in from {ipAddress}");

// Registro estructurado con propiedades explícitas
logger.LogStructured(LogLevel.Error, "Payment failed",
    properties: [new("OrderId", orderId), new("Amount", amount)],
    exception: ex);
```

### Mensajes Generados

El atributo `[PicoLogMessage]` en métodos parciales estáticos emite métodos de extensión fuertemente tipados y compatibles con AOT:

```csharp
public static partial class AppLogs
{
    [PicoLogMessage(LogLevel.Info, EventId = 1001, Message = "Order {OrderId} placed")]
    public static partial void OrderPlaced(this ILogger logger, string orderId);

    [PicoLogMessage(LogLevel.Error, EventId = 2001, Message = "Payment failed")]
    public static partial void PaymentFailed(this ILogger logger, string orderId, Exception ex);
}

// Uso
logger.OrderPlaced("ORD-12345");
logger.PaymentFailed("ORD-12345", ex);
```

### Sumideros Incluidos

```csharp
var sinks = new List<ILogSink>
{
    new ConsoleSink(new ConsoleFormatter()),          // Consola simple
    new ColoredConsoleSink(new ConsoleFormatter()),    // Codificado por colores según el nivel
    new FileSink(new ConsoleFormatter(),               // Salida a archivo por lotes
        new FileSinkOptions { FilePath = "app.log", BatchSize = 100 })
};
using var factory = new LoggerFactory(sinks);
```

### Sumideros Personalizados

```csharp
public sealed class CustomSink : ILogSink
{
    public Task WriteAsync(LogEntry entry, CancellationToken ct = default)
    {
        // Escriba en su sistema de backend
        return Task.CompletedTask;
    }

    public void Dispose() { }
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
```

---

## Integración de Módulos

Cuando `PicoDI`, `PicoCfg.DI` y `PicoLog.DI` están todos referenciados, la conexión ocurre automáticamente:

1. El generador de código de cada módulo registra un configurador basado en `ModuleInitializer`
2. `new SvcContainer()` ejecuta todos los configuradores mediante `SvcContainerAutoConfiguration.TryApplyConfiguration`
3. No se necesita configuración manual

### APIs de Integración con DI

**PicoCfg.DI** &mdash; extensiones en `ISvcContainer`:
- `RegisterCfgRoot(ICfgRoot root)` &mdash; registra `ICfgRoot` e `ICfg`
- `RegisterCfgTransient/Scoped/Singleton<T>(string? section)` &mdash; vincula POCOs desde configuración
- `RegisterCfgOptionsSingleton/Scoped<T>(string? section)` &mdash; soporte para opciones tipadas

**PicoLog.DI** &mdash; extensión en `ISvcContainer`:
- `AddPicoLog(Action<LoggingOptions> configure)` &mdash; registra `ILoggerFactory` e `ILogger<>`
- `WriteTo.Console()` / `.ColoredConsole()` / `.File(path)` / `.Sink(custom)` &mdash; configuración de sumideros
- `ReadFrom.RegisteredSinks()` &mdash; incluye instancias de `ILogSink` registradas en DI

### Ejemplo Mínimo Combinado

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

## Filosofía de Diseño

**克制 (Moderación)** — Solo DI, Config y Logging. Sin frameworks web, sin ORM, sin colas de mensajes. Si no es infraestructura universal, no pertenece aquí. PicoHex es el denominador común que toda aplicación necesita — ni más, ni menos.

**专注 (Enfoque)** — Cada módulo hace una sola cosa. PicoDI es un contenedor, no un localizador de servicios. PicoCfg es configuración, no un sistema de banderas de funcionalidad. PicoLog es registro, no una tubería de telemetría. Especialización profunda sobre generalidad superficial.

**优雅 (Elegancia)** — Las APIs son mínimas. Los generadores de código realizan las conexiones en tiempo de compilación. El desarrollador escribe código directo; las herramientas manejan la complejidad. `new SvcContainer()` reemplaza más de 100 líneas de ceremonia con `Host.CreateDefaultBuilder()`.

**高效 (Eficiencia)** — AOT First no es una ocurrencia tardía, es la base. Cero reflexión. Cero sobrecarga en ejecución. Todo lo que puede resolverse en compilación se resuelve en compilación. Binarios mínimos, arranques en frío rápidos, rendimiento predecible.

---

## Casos de Uso

| Escenario | PicoDI | PicoCfg | PicoLog | Por qué |
|---|---|---|---|---|
| **CLI Tools** | Opcional | Esencial | Esencial | Analizar args/config, controlar verbosidad. DI está disponible si la herramienta crece. |
| **Serverless / Lambda** | Esencial | Esencial | Esencial | El arranque en frío es el cuello de botella. DI compilada a AOT con rutas de resolución pregeneradas. |
| **WASM / Blazor** | Esencial | Esencial | Esencial | El tamaño de descarga importa. 11 paquetes, optimizado para recorte, sin inflado en ejecución. |
| **Embedded / IoT** | Cuando se necesite | Esencial | Esencial | Dispositivos con recursos limitados. Binario mínimo, cero asignaciones donde sea posible. |

---

## Paquetes

| Paquete | Descripción | NuGet |
|---|---|---|
| **PicoDI** | Contenedor DI sin reflexión con generación de código en compilación | [![NuGet](https://img.shields.io/nuget/v/PicoDI)](https://nuget.org/packages/PicoDI) |
| **PicoDI.Abs** | Abstracciones para PicoDI (`ISvcContainer`, `ISvcScope`, `SvcDescriptor`) | [![NuGet](https://img.shields.io/nuget/v/PicoDI.Abs)](https://nuget.org/packages/PicoDI.Abs) |
| **PicoDI.Gen** | Generador Roslyn: registro y resolución en compilación | [![NuGet](https://img.shields.io/nuget/v/PicoDI.Gen)](https://nuget.org/packages/PicoDI.Gen) |
| **PicoCfg** | Gestión de configuración asíncrona con modelo de proveedores | [![NuGet](https://img.shields.io/nuget/v/PicoCfg)](https://nuget.org/packages/PicoCfg) |
| **PicoCfg.Abs** | Abstracciones para PicoCfg (`ICfg`, `ICfgRoot`, `ICfgSection`) | [![NuGet](https://img.shields.io/nuget/v/PicoCfg.Abs)](https://nuget.org/packages/PicoCfg.Abs) |
| **PicoCfg.Gen** | Generador de código para enlace de configuración tipada (`CfgBind.Bind<T>`) | [![NuGet](https://img.shields.io/nuget/v/PicoCfg.Gen)](https://nuget.org/packages/PicoCfg.Gen) |
| **PicoCfg.DI** | Integración PicoDI para PicoCfg (`RegisterCfgRoot`, `RegisterCfgSingleton`) | [![NuGet](https://img.shields.io/nuget/v/PicoCfg.DI)](https://nuget.org/packages/PicoCfg.DI) |
| **PicoLog** | Registro estructurado con plantillas de mensaje en compilación | [![NuGet](https://img.shields.io/nuget/v/PicoLog)](https://nuget.org/packages/PicoLog) |
| **PicoLog.Abs** | Abstracciones para PicoLog (`ILogger`, `ILogSink`, `LogLevel`) | [![NuGet](https://img.shields.io/nuget/v/PicoLog.Abs)](https://nuget.org/packages/PicoLog.Abs) |
| **PicoLog.Gen** | Generador de código para métodos `[PicoLogMessage]` | [![NuGet](https://img.shields.io/nuget/v/PicoLog.Gen)](https://nuget.org/packages/PicoLog.Gen) |
| **PicoLog.DI** | Integración PicoDI para PicoLog (`AddPicoLog`, `WriteTo`, `ReadFrom`) | [![NuGet](https://img.shields.io/nuget/v/PicoLog.DI)](https://nuget.org/packages/PicoLog.DI) |

---

## Comparación

### Frente a Autofac

Autofac es un contenedor DI maduro y rico en funciones con inyección de propiedades, decoradores, interceptores y módulos. PicoDI adopta el enfoque opuesto: **cero reflexión en ejecución, registro solo en compilación, seguro para AOT por diseño**. Si necesita flexibilidad en tiempo de ejecución, use Autofac. Si necesita **AOT y mínima sobrecarga**, use PicoDI.

### Frente a Lamar

Lamar es un contenedor DI de alto rendimiento que usa generación de código en ejecución (`DynamicAssembly` + emisión IL). PicoDI usa generadores de código Roslyn para generar código **en compilación**. Lamar soporta más funciones (intercepción, decoración); PicoDI es mínimo y prioriza AOT.

### Frente a Serilog

Serilog es el estándar de oro para registro estructurado en .NET con un vasto ecosistema de sumideros. PicoLog no reemplaza a Serilog: es una **alternativa ligera** para proyectos que valoran la compatibilidad con AOT y las dependencias mínimas por encima de la variedad de sumideros. El generador de código de PicoLog para plantillas de mensaje ofrece una calidad de registro estructurado comparable.

### Frente a Microsoft.Extensions

PicoHex no es una extensión de `Microsoft.Extensions`, es una **alternativa**. Diseñada desde cero para Native AOT, con generación de código en compilación en lugar de reflexión en ejecución. Si está construyendo aplicaciones ASP.NET tradicionales, quédese con Microsoft.Extensions. Si está construyendo **herramientas CLI, funciones serverless, aplicaciones WASM o sistemas embebidos**, PicoHex está hecho para usted.

---

## Licencia y Contribuciones

Licencia MIT. Repositorio: [https://github.com/PicoHex/PicoHex](https://github.com/PicoHex/PicoHex)
