# PicoHex

**Минимальная универсальная инфраструктура для .NET с приоритетом Native AOT**

Минимальная универсальная инфраструктура для production-grade .NET приложений &mdash; три модуля, одиннадцать пакетов, нулевое отражение во время выполнения.

[![NuGet](https://img.shields.io/nuget/v/PicoDI)](https://nuget.org/packages/PicoDI)
[![License: MIT](https://img.shields.io/badge/License-MIT-green.svg)](LICENSE)
[![CI](https://github.com/PicoHex/PicoHex/actions/workflows/ci.yml/badge.svg)](https://github.com/PicoHex/PicoHex/actions/workflows/ci.yml)



[English](README.md) | [简体中文](README.zh.md) | [日本語](README.ja.md) | [Español](README.es.md) | [Português](README.pt.md) | [繁體中文](README.zh-tw.md) | [한국어](README.ko.md) | [Français](README.fr.md) | [Deutsch](README.de.md) | [Русский](README.ru.md)

---

## Вычислительная модель

```
Конфигурация  ──→  Внедрение зависимостей  ──→  Логирование
   (Ввод)               (Ядро)                    (Вывод)
```

Любое приложение читает конфигурацию, собирает внутренние компоненты и выдаёт результат. **PicoHex** — минимальная реализация этих трёх мета-операций для .NET Native AOT, спроектированная с нуля под кодогенерацию на этапе компиляции вместо runtime-отражения.

---

## Почему PicoHex

| | Microsoft.Extensions | PicoHex |
|---|---|---|
| **Пакеты** | 40+ | 11 |
| **Runtime-отражение** | Тяжёлое (`Activator.CreateInstance`, деревья выражений) | Отсутствует (source generators) |
| **Готовность к AOT** | Требует ручного включения и тонкой настройки | AOT First — компилируется нативно «из коробки» |
| **HostBuilder** | Обязателен для связки DI + Config + Logging | Не нужен — достаточно `new SvcContainer()` |
| **Интеграция модулей** | Регистрация во время выполнения (`IServiceCollection`) | На этапе компиляции через `ModuleInitializer` + source generators |
| **Холодный старт** | Медленный (JIT + отражение) | Быстрый (предварительно сгенерированные пути кода) |
| **Размер бинарника** | Большой (тянет много сборок) | Минимальный (поддаётся обрезке, дружелюбен к линковщику) |

---

## Производительность

Бенчмарки выполнены на **.NET 10.0.5, Windows 10, X64, Native AOT, Release mode**.

### PicoDI vs Microsoft.Extensions.DependencyInjection

**20/20 побед, в среднем в 2.83× быстрее, максимум 4.00× (DeepChain × Transient).**

| Сценарий | PicoDI (нс) | MsDI (нс) | Ускорение |
|---|---|---|---|
| **DeepChain × Transient** | 156.7 | 626.9 | **4.00×** |
| **NoDependency × Singleton** | 14.2 | 55.1 | **3.89×** |
| **MultipleResolutions × Singleton** | 1 523.6 | 5 373.9 | **3.53×** |
| **MultipleResolutions × Transient** | 4 672.4 | 16 402.1 | **3.51×** |
| **NoDependency × Transient** | 27.7 | 97.1 | **3.50×** |
| ContainerSetup | 739.9 | 1 919.1 | 2.59× |
| SingleResolution × Transient | 55.3 | 187.8 | 3.39× |
| ScopeCreation | 94.1 | 104.5 | 1.11× |

**AOT-бинарник: 3 087.5 КБ**

### PicoCfg vs Microsoft.Extensions.Configuration

**В 1.35–1.57× быстрее на смешанных нагрузках.**

| Сценарий | PicoCfg (нс) | MsConfig (нс) | Ускорение |
|---|---|---|---|
| Mixed n=100, p=2, l=1 | 5 920.9 | 9 273.9 | **1.57×** |
| Mixed n=100, p=2, l=10 | 29 831.8 | 40 218.9 | **1.35×** |

**AOT-бинарник: 2 476.5 КБ**

### PicoLog vs Microsoft.Extensions.Logging

PicoLog формирует более насыщенные записи лога (временная метка, категория, области видимости, свойства), поэтому асинхронный путь передачи данных сопоставим с лёгкой передачей строковых каналов Microsoft. **Контрольные бенчмарки** измеряют эффективность без накладных расходов очереди/приёмника:

| Контрольный бенчмарк | N=1 | N=10 | N=100 |
|---|---|---|---|
| **TimestampNowOnly** | 4.15× | 5.05× | 4.54× |
| **LogEntryAllocateOnly** | 8.48× | 23.48× | 20.11× |

**AOT-бинарник: 3 020 КБ**

---

## Быстрый старт

### Только конфигурация

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

### Только DI

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

### Только логирование

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

Контейнер внедрения зависимостей без отражения с кодогенерацией на этапе компиляции.

### Регистрация

Все методы `Register*` возвращают `ISvcContainer` для построения цепочек вызовов.

```csharp
var container = new SvcContainer();

// На основе фабрики (всегда работает, source gen не требуется)
container.RegisterSingleton<IService>(scope => new Service(scope.GetService<IDep>()));
container.RegisterScoped<IRepository>(scope => new Repository());
container.RegisterTransient<IValidator>(scope => new Validator());

// Готовый экземпляр
container.RegisterSingle<IClock>(SystemClock.Instance);

// Открытые обобщения
container.Register(typeof(IRepository<>), typeof(SqlRepository<>), SvcLifetime.Scoped);

// На основе типов (требуется source generator PicoDI.Gen)
container.RegisterSingleton<IService, Service>();

// Фоновые службы
container.RegisterHostedSvc<BackgroundWorker>();

container.Build();  // Заморозка регистраций
```

### Разрешение зависимостей

```csharp
using var scope = container.CreateScope();

// Типизированное разрешение (мгновенный поиск через сгенерированные методы Resolve.*)
var svc = scope.GetService<IService>();
var repos = scope.GetServices<IRepository>();

// Разрешение по типу
var instance = scope.GetService(typeof(IService));
```

### Время жизни

| Время жизни | Создание | Освобождение |
|---|---|---|
| **Transient** | Новый экземпляр каждый раз | Отслеживается областью разрешения, освобождается в порядке LIFO |
| **Scoped** | Один раз на область | Освобождается при освобождении области |
| **Singleton** | Один раз на контейнер | Освобождается при освобождении контейнера |

### Source Generator (PicoDI.Gen)

Добавьте `PicoDI.Gen` как анализатор для включения регистраций на основе типов на этапе компиляции:

```xml
<PackageReference Include="PicoDI.Gen" PrivateAssets="all" />
```

Генератор сканирует все вызовы `Register*` и создаёт:

- **Метод расширения `ConfigureGeneratedServices()`** со встроенными фабричными делегатами (без отражения)
- **Типизированные методы `Resolve.*`** для путей разрешения без поиска
- **Обнаружение циклических зависимостей на этапе компиляции**
- **Метаданные открытых обобщений** для обнаружения между сборками
- **Автоконфигуратор `[ModuleInitializer]`**, регистрирующийся через `SvcContainerAutoConfiguration`

**До** (ваш код):
```csharp
var container = new SvcContainer();
container.RegisterSingleton<IService, Service>();
container.Build();
```

**После** (сгенерировано):
```csharp
// Автоматически сгенерировано PicoDI.Gen
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

Опциональный построитель с плавным интерфейсом для фоновых служб:

```csharp
using var hostBuilder = new SvcHostBuilder();
var host = await hostBuilder
    .ConfigureServices(container =>
    {
        container.RegisterSingleton<IService>(_ => new Service());
        container.RegisterHostedSvc<Worker>();
    })
    .BuildAsync();

// Приложение работает до остановки
await host.StopAsync();
```

---

## PicoCfg

Асинхронное управление конфигурацией с моделью провайдеров.

### Модель провайдеров

```
Источники  ──→  Провайдеры  ──→  Корень  ──→  Потребитель
```

Источники определяют **как** создаётся конфигурация. Провайдеры — материализованные экземпляры. Корень объединяет снимки провайдеров в единое представление. Потребители запрашивают данные через `TryGetValue` или `GetValue`.

### Встроенные источники

| Источник | Описание |
|---|---|
| **Dictionary** | Пары «ключ-значение» в памяти |
| **Environment Variables** | Переменные окружения ОС, фильтрация по префиксу, преобразование `__` → `:` |
| **Command Line** | `--key=value`, `--key value`, `-key value`, `/key value` |
| **Stream** | Построчный разбор текста `key=value` с отслеживанием файла |
| **File Watching** | Автоперезагрузка при изменении файла с подавлением повторных событий |
| **Chained** | Обращение к другому экземпляру `ICfg` как к запасному варианту |
| **KeyPerFile** | В стиле Kubernetes ConfigMap — имя файла = ключ, содержимое = значение |

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

### Привязка через Source Generator

Добавьте `PicoCfg.Gen` как анализатор. Source generator обнаруживает цели привязки на этапе компиляции по местам вызовов `CfgBind.Bind<T>` / `CfgBind.TryBind<T>` / `CfgBind.BindInto<T>` — атрибуты не требуются. Сгенерированный код регистрирует строго типизированные связыватели, которые разрешают значения конфигурации по ключевым путям без runtime-отражения.

```csharp
using PicoCfg;

public sealed class AppSettings
{
    public string Name { get; init; }
    public int MaxRetries { get; init; } = 3;
    public bool EnableFeature { get; init; }
}

// Точка вызова запускает кодогенерацию для AppSettings
var settings = CfgBind.Bind<AppSettings>(cfg, "App");
```

---

## PicoLog

Структурированное логирование с шаблонами сообщений на этапе компиляции.

### Уровни логирования

| Уровень | Значение | Назначение |
|---|---|---|
| **Emergency** | 0 | Система неработоспособна |
| **Alert** | 1 | Требуется немедленное действие |
| **Critical** | 2 | Критическое состояние |
| **Error** | 3 | Ошибка |
| **Warning** | 4 | Предупреждение |
| **Notice** | 5 | Нормально, но значимо |
| **Info** | 6 | Информационные сообщения |
| **Debug** | 7 | Отладочные сообщения |
| **Trace** | 8 | Детальная диагностика |
| **None** | 255 | Отключает всё логирование |

### Шаблоны сообщений

Поддержка как строковой интерполяции, так и `FormattableString` с именованными параметрами для структурированного логирования:

```csharp
logger.Info($"Processing order {orderId} for {customer}");
logger.Log(LogLevel.Info, $"User {user} logged in from {ipAddress}");

// Структурированное логирование с явными свойствами
logger.LogStructured(LogLevel.Error, "Payment failed",
    properties: [new("OrderId", orderId), new("Amount", amount)],
    exception: ex);
```

### Сообщения через Source Generator

Атрибут `[PicoLogMessage]` на статических частичных методах порождает строго типизированные, AOT-совместимые методы расширения:

```csharp
public static partial class AppLogs
{
    [PicoLogMessage(LogLevel.Info, EventId = 1001, Message = "Order {OrderId} placed")]
    public static partial void OrderPlaced(this ILogger logger, string orderId);

    [PicoLogMessage(LogLevel.Error, EventId = 2001, Message = "Payment failed")]
    public static partial void PaymentFailed(this ILogger logger, string orderId, Exception ex);
}

// Использование
logger.OrderPlaced("ORD-12345");
logger.PaymentFailed("ORD-12345", ex);
```

### Встроенные приёмники (sinks)

```csharp
var sinks = new List<ILogSink>
{
    new ConsoleSink(new ConsoleFormatter()),          // Обычная консоль
    new ColoredConsoleSink(new ConsoleFormatter()),    // Цветовая кодировка по уровню
    new FileSink(new ConsoleFormatter(),               // Пакетный вывод в файл
        new FileSinkOptions { FilePath = "app.log", BatchSize = 100 })
};
using var factory = new LoggerFactory(sinks);
```

### Пользовательские приёмники

```csharp
public sealed class CustomSink : ILogSink
{
    public Task WriteAsync(LogEntry entry, CancellationToken ct = default)
    {
        // Запись в ваше хранилище
        return Task.CompletedTask;
    }

    public void Dispose() { }
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
```

---

## Интеграция модулей

Когда одновременно указаны ссылки на `PicoDI`, `PicoCfg.DI` и `PicoLog.DI`, связывание происходит автоматически:

1. Source generator каждого модуля регистрирует конфигуратор на основе `ModuleInitializer`
2. `new SvcContainer()` запускает все конфигураторы через `SvcContainerAutoConfiguration.TryApplyConfiguration`
3. Ручное связывание не требуется

### API интеграции с DI

**PicoCfg.DI** — расширения на `ISvcContainer`:
- `RegisterCfgRoot(ICfgRoot root)` — регистрирует `ICfgRoot` и `ICfg`
- `RegisterCfgTransient/Scoped/Singleton<T>(string? section)` — привязка POCO из конфигурации
- `RegisterCfgOptionsSingleton/Scoped<T>(string? section)` — поддержка типизированных опций

**PicoLog.DI** — расширение на `ISvcContainer`:
- `AddPicoLog(Action<LoggingOptions> configure)` — регистрирует `ILoggerFactory` и `ILogger<>`
- `WriteTo.Console()` / `.ColoredConsole()` / `.File(path)` / `.Sink(custom)` — настройка приёмников
- `ReadFrom.RegisteredSinks()` — подключение экземпляров `ILogSink`, зарегистрированных в DI

### Минимальный полный пример

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

## Философия дизайна

**克制 (Сдержанность — Restraint)** — Только DI, Config, Logging. Ни веб-фреймворка, ни ORM, ни очередей сообщений. Если это не универсальная инфраструктура — здесь этому не место. PicoHex — общий знаменатель, необходимый любому приложению. Не больше, не меньше.

**专注 (Сосредоточенность — Focus)** — Каждый модуль делает одно дело. PicoDI — контейнер, а не сервис-локатор. PicoCfg — конфигурация, а не система флагов. PicoLog — логирование, а не телеметрический конвейер. Глубокая специализация вместо поверхностной универсальности.

**优雅 (Элегантность — Elegance)** — API минимальны. Source generators выполняют связывание на этапе компиляции. Разработчик пишет прямой код — инструментарий берёт на себя сложность. `new SvcContainer()` заменяет 100+ строк церемоний `Host.CreateDefaultBuilder()`.

**高效 (Эффективность — Efficiency)** — AOT First — не запоздалая мысль, а фундамент. Никакого отражения. Никаких накладных расходов во время выполнения. Всё, что можно разрешить на этапе компиляции, разрешается на этапе компиляции. Минимальные бинарники, быстрый холодный старт, предсказуемая производительность.

---

## Сценарии использования

| Сценарий | PicoDI | PicoCfg | PicoLog | Зачем |
|---|---|---|---|---|
| **CLI-инструменты** | Опционально | Обязательно | Обязательно | Разбор аргументов/конфига, управление выводом. DI — если инструмент разрастётся. |
| **Serverless / Lambda** | Обязательно | Обязательно | Обязательно | Холодный старт — узкое место. AOT-скомпилированный DI с предварительно сгенерированными путями разрешения. |
| **WASM / Blazor** | Обязательно | Обязательно | Обязательно | Размер загрузки имеет значение. 11 пакетов, дружелюбно к обрезке, без раздувания во время выполнения. |
| **Embedded / IoT** | Когда нужно | Обязательно | Обязательно | Устройства с ограниченными ресурсами. Минимальный бинарник, где возможно — без выделений памяти. |

---

## Пакеты

| Пакет | Описание | NuGet |
|---|---|---|
| **PicoDI** | Контейнер DI без отражения с кодогенерацией на этапе компиляции | [![NuGet](https://img.shields.io/nuget/v/PicoDI)](https://nuget.org/packages/PicoDI) |
| **PicoDI.Abs** | Абстракции для PicoDI (`ISvcContainer`, `ISvcScope`, `SvcDescriptor`) | [![NuGet](https://img.shields.io/nuget/v/PicoDI.Abs)](https://nuget.org/packages/PicoDI.Abs) |
| **PicoDI.Gen** | Source generator Roslyn — регистрация и разрешение на этапе компиляции | [![NuGet](https://img.shields.io/nuget/v/PicoDI.Gen)](https://nuget.org/packages/PicoDI.Gen) |
| **PicoCfg** | Асинхронное управление конфигурацией с моделью провайдеров | [![NuGet](https://img.shields.io/nuget/v/PicoCfg)](https://nuget.org/packages/PicoCfg) |
| **PicoCfg.Abs** | Абстракции для PicoCfg (`ICfg`, `ICfgRoot`, `ICfgSection`) | [![NuGet](https://img.shields.io/nuget/v/PicoCfg.Abs)](https://nuget.org/packages/PicoCfg.Abs) |
| **PicoCfg.Gen** | Source generator для типизированной привязки конфигурации (`CfgBind.Bind<T>`) | [![NuGet](https://img.shields.io/nuget/v/PicoCfg.Gen)](https://nuget.org/packages/PicoCfg.Gen) |
| **PicoCfg.DI** | Интеграция PicoCfg с PicoDI (`RegisterCfgRoot`, `RegisterCfgSingleton`) | [![NuGet](https://img.shields.io/nuget/v/PicoCfg.DI)](https://nuget.org/packages/PicoCfg.DI) |
| **PicoLog** | Структурированное логирование с шаблонами сообщений на этапе компиляции | [![NuGet](https://img.shields.io/nuget/v/PicoLog)](https://nuget.org/packages/PicoLog) |
| **PicoLog.Abs** | Абстракции для PicoLog (`ILogger`, `ILogSink`, `LogLevel`) | [![NuGet](https://img.shields.io/nuget/v/PicoLog.Abs)](https://nuget.org/packages/PicoLog.Abs) |
| **PicoLog.Gen** | Source generator для методов с атрибутом `[PicoLogMessage]` | [![NuGet](https://img.shields.io/nuget/v/PicoLog.Gen)](https://nuget.org/packages/PicoLog.Gen) |
| **PicoLog.DI** | Интеграция PicoLog с PicoDI (`AddPicoLog`, `WriteTo`, `ReadFrom`) | [![NuGet](https://img.shields.io/nuget/v/PicoLog.DI)](https://nuget.org/packages/PicoLog.DI) |

---

## Сравнение

### vs Autofac

Autofac — зрелый, многофункциональный DI-контейнер с внедрением свойств, декораторами, перехватчиками и модулями. PicoDI использует противоположный подход: **никакого runtime-отражения, только регистрация на этапе компиляции, безопасен для AOT по своей архитектуре**. Если вам нужна гибкость во время выполнения — используйте Autofac. Если вам нужны **AOT и минимальные накладные расходы** — используйте PicoDI.

### vs Lamar

Lamar — производительный DI-контейнер, использующий генерацию кода во время выполнения (`DynamicAssembly` + IL emit). PicoDI использует source generators Roslyn для **компиляционной** генерации кода. Lamar поддерживает больше возможностей (перехват, декорирование); PicoDI минималистичен и создан с приоритетом AOT.

### vs Serilog

Serilog — золотой стандарт структурированного логирования в .NET с огромной экосистемой приёмников. PicoLog — не замена Serilog, а **лёгкая альтернатива** для проектов, где важны AOT-совместимость и минимальные зависимости, а не разнообразие приёмников. Source generator PicoLog для шаблонов сообщений обеспечивает сопоставимое качество структурированного логирования.

### vs Microsoft.Extensions

PicoHex — не расширение `Microsoft.Extensions`, а **альтернатива**. Спроектирован с нуля для Native AOT, с кодогенерацией на этапе компиляции вместо runtime-отражения. Если вы создаёте традиционные ASP.NET-приложения — оставайтесь на Microsoft.Extensions. Если вы создаёте **CLI-инструменты, serverless-функции, WASM-приложения или встраиваемые системы** — PicoHex создан для вашего сценария.

---

## Лицензия и вклад

Лицензия MIT. Репозиторий: [https://github.com/PicoHex/PicoHex](https://github.com/PicoHex/PicoHex)
