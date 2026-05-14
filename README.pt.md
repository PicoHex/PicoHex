# PicoHex

**A infraestrutura mínima universal para .NET com AOT First**

A infraestrutura universal mínima para aplicações .NET de nível de produção &mdash; três módulos, onze pacotes, zero reflexão em tempo de execução.

[![NuGet](https://img.shields.io/nuget/v/PicoDI)](https://nuget.org/packages/PicoDI)
[![License: MIT](https://img.shields.io/badge/License-MIT-green.svg)](LICENSE)
[![CI](https://github.com/PicoHex/PicoHex/actions/workflows/ci.yml/badge.svg)](https://github.com/PicoHex/PicoHex/actions/workflows/ci.yml)



[English](README.md) | [简体中文](README.zh.md) | [日本語](README.ja.md) | [Español](README.es.md) | [Português](README.pt.md) | [繁體中文](README.zh-tw.md) | [한국어](README.ko.md) | [Français](README.fr.md) | [Deutsch](README.de.md) | [Русский](README.ru.md)

---

## Modelo Computacional

```
Configuração  ──→  Injeção de Dependência  ──→  Geração de Logs
   (Entrada)            (Núcleo)                    (Saída)
```

Toda aplicação lê configuração, monta seus componentes internos e produz saída. **PicoHex** é a implementação mínima dessas três meta-operações para .NET Native AOT &mdash; projetada desde o início para geração de código em tempo de compilação, em vez de reflexão em tempo de execução.

---

## Por que PicoHex?

| | Microsoft.Extensions | PicoHex |
|---|---|---|
| **Pacotes** | 40+ | 11 |
| **Reflexão em runtime** | Pesada (`Activator.CreateInstance`, árvores de expressão) | Zero (source generators) |
| **Pronto para AOT** | Exige opt-in e configuração cuidadosa | AOT First &mdash; compila nativamente de imediato |
| **HostBuilder obrigatório** | Sim &mdash; necessário para integrar DI + Config + Logging | Não &mdash; `new SvcContainer()` é tudo que você precisa |
| **Integração de módulos** | Registro em runtime (`IServiceCollection`) | Em tempo de compilação via `ModuleInitializer` + source generators |
| **Cold start** | Rápido (JIT) | Rápido (caminhos de código pré-gerados) |
| **Tamanho do binário** | Grande (puxa vários assemblies) | Mínimo (trim-friendly, otimizado para linker) |

---

## Começo Rápido

### Apenas Configuração

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

### Apenas DI

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

### Apenas Logging

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

### Tudo Junto

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
var logger = container.CreateScope().GetService<ILogger<Program>>();
```

---

## Pacotes

| Pacote | Descrição |
|---|---|
| **PicoDI** | Contêiner DI sem reflexão |
| **PicoDI.Abs** | Abstrações de DI |
| **PicoDI.Gen** | Source generator para registro em tempo de compilação |
| **PicoCfg** | Configuração assíncrona |
| **PicoCfg.Abs** | Abstrações de configuração |
| **PicoCfg.Gen** | Source generator para binding tipado |
| **PicoCfg.DI** | Integração com DI para PicoCfg |
| **PicoLog** | Logging estruturado |
| **PicoLog.Abs** | Abstrações de logging |
| **PicoLog.Gen** | Source generator `[PicoLogMessage]` |
| **PicoLog.DI** | Integração com DI para PicoLog |

---

## Filosofia de Design

**克制 (Restraint) / Contenção** — Apenas DI, Config e Logging. Nada de Web framework, ORM ou fila de mensagens. Se não é infraestrutura universal, não pertence aqui. PicoHex é o denominador comum que toda aplicação precisa &mdash; nada mais, nada menos.

**专注 (Focus) / Foco** — Cada módulo faz uma coisa. PicoDI é um contêiner, não um service locator. PicoCfg é configuração, não um sistema de feature flags. PicoLog é logging, não um pipeline de telemetria. Especialização profunda sobre generalidade superficial.

**优雅 (Elegance) / Elegância** — `new SvcContainer()` substitui 100+ linhas de cerimônia do `Host.CreateDefaultBuilder()`. Source generators fazem a conexão em tempo de compilação. O desenvolvedor escreve código direto; as ferramentas cuidam da complexidade.

**高效 (Efficiency) / Eficiência** — AOT First. Zero reflexão em runtime. Tudo que pode ser resolvido em tempo de compilação é resolvido em tempo de compilação. Binários mínimos, cold starts rápidos, performance previsível.

---

## Saiba Mais

- [PicoDI](PicoDI/README.md) — Contêiner DI, registro, source generator
- [PicoCfg](PicoCfg/README.md) — Provedores de configuração, binding, monitoramento de arquivos
- [PicoLog](PicoLog/README.md) — Logging estruturado, sinks, modelos de mensagem
- [Contribuindo](CONTRIBUTING.md)
- [Segurança](SECURITY.md)

---

Licença MIT. [https://github.com/PicoHex/PicoHex](https://github.com/PicoHex/PicoHex)
