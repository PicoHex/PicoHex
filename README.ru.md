# PicoHex

**Минимальная универсальная инфраструктура для .NET с приоритетом Native AOT**

Минимальная универсальная инфраструктура для production-grade .NET приложений &mdash; пять модулей, нулевое отражение во время выполнения.

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

await using var scope = container.CreateScope();
var svc = scope.GetService<IService>();
```

PicoDI также поддерживает **AOP/перехватчики на этапе компиляции** — добавьте .InterceptBy<TInterceptor>() после Register(), и генератор исходного кода создаст классы-декораторы во время сборки. [Подробнее →](PicoDI/README.md#interceptor--aop-compile-time-decorators)

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

### Всё вместе

```shell
dotnet add package PicoCfg.DI
dotnet add package PicoLog.DI
```

```csharp
using PicoDI;
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

PicoDI также поддерживает **AOP/перехватчики на этапе компиляции** — добавьте .InterceptBy<TInterceptor>() после Register(), и генератор исходного кода создаст классы-декораторы во время сборки. [Подробнее →](PicoDI/README.md#interceptor--aop-compile-time-decorators)

---

## Пакеты

| Пакет | Описание |
|---|---|
| **PicoDI** | Контейнер DI без отражения |
| **PicoDI.Abs** | Абстракции для DI |
| **PicoDI.Gen** | Source generator для регистрации на этапе компиляции |
| **PicoCfg** | Асинхронная конфигурация |
| **PicoCfg.Abs** | Абстракции конфигурации |
| **PicoCfg.Gen** | Source generator для типизированной привязки |
| **PicoCfg.DI** | Интеграция PicoCfg с PicoDI |
| **PicoLog** | Структурированное логирование |
| **PicoLog.Abs** | Абстракции логирования |
| **PicoLog.Gen** | Source generator для `[PicoLogMessage]` |
| **PicoLog.DI** | Интеграция PicoLog с PicoDI |

---

## Философия дизайна

**克制 (Сдержанность — Restraint)** — Только DI, Config, Logging. Ни веб-фреймворка, ни ORM, ни очередей сообщений. Если это не универсальная инфраструктура — здесь этому не место. PicoHex — общий знаменатель, необходимый любому приложению. Не больше, не меньше.

**专注 (Сосредоточенность — Focus)** — Каждый модуль делает одно дело. PicoDI — контейнер, а не сервис-локатор. PicoCfg — конфигурация, а не система флагов. PicoLog — логирование, а не телеметрический конвейер. Глубокая специализация вместо поверхностной универсальности.

**优雅 (Элегантность — Elegance)** — API минимальны. Source generators выполняют связывание на этапе компиляции. Разработчик пишет прямой код — инструментарий берёт на себя сложность. `new SvcContainer()` заменяет 100+ строк церемоний `Host.CreateDefaultBuilder()`.

**高效 (Эффективность — Efficiency)** — AOT First — не запоздалая мысль, а фундамент. Никакого отражения. Никаких накладных расходов во время выполнения. Всё, что можно разрешить на этапе компиляции, разрешается на этапе компиляции. Минимальные бинарники, быстрый холодный старт, предсказуемая производительность.

---

## Подробнее

- [PicoDI](PicoDI/README.md) — Контейнер DI, регистрация, source generator
- [PicoCfg](PicoCfg/README.md) — Провайдеры конфигурации, привязка, отслеживание файлов
- [PicoLog](PicoLog/README.md) — Структурированное логирование, приёмники, шаблоны сообщений
- [Contributing](CONTRIBUTING.md) — Участие в разработке
- [Security](SECURITY.md) — Безопасность

---

Лицензия MIT. [https://github.com/PicoHex/PicoHex](https://github.com/PicoHex/PicoHex)