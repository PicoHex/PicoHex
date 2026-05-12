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
| **Cold start** | Lento (JIT + reflexão) | Rápido (caminhos de código pré-gerados) |
| **Tamanho do binário** | Grande (puxa vários assemblies) | Mínimo (trim-friendly, otimizado para linker) |

---

## Performance

Benchmarks executados em **.NET 10.0.5, Windows 10, X64, Native AOT, modo Release**.

### PicoDI vs Microsoft.Extensions.DependencyInjection

**20/20 vitórias, média 2,83&times; mais rápido, máximo de 4,00&times; (DeepChain &times; Transient).**

| Cenário | PicoDI (ns) | MsDI (ns) | Ganho |
|---|---|---|---|
| **DeepChain &times; Transient** | 156,7 | 626,9 | **4,00&times;** |
| **NoDependency &times; Singleton** | 14,2 | 55,1 | **3,89&times;** |
| **MultipleResolutions &times; Singleton** | 1.523,6 | 5.373,9 | **3,53&times;** |
| **MultipleResolutions &times; Transient** | 4.672,4 | 16.402,1 | **3,51&times;** |
| **NoDependency &times; Transient** | 27,7 | 97,1 | **3,50&times;** |
| ContainerSetup | 739,9 | 1.919,1 | 2,59&times; |
| SingleResolution &times; Transient | 55,3 | 187,8 | 3,39&times; |
| ScopeCreation | 94,1 | 104,5 | 1,11&times; |

**Binário AOT: 3.087,5 KB**

### PicoCfg vs Microsoft.Extensions.Configuration

**1,35&times; a 1,57&times; mais rápido em cargas de trabalho mistas.**

| Cenário | PicoCfg (ns) | MsConfig (ns) | Ganho |
|---|---|---|---|
| Misto n=100, p=2, l=1 | 5.920,9 | 9.273,9 | **1,57&times;** |
| Misto n=100, p=2, l=10 | 29.831,8 | 40.218,9 | **1,35&times;** |

**Binário AOT: 2.476,5 KB**

### PicoLog vs Microsoft.Extensions.Logging

PicoLog constrói entradas de log mais ricas (timestamp, categoria, escopos, propriedades), por isso o caminho de handoff assíncrono é comparável ao handoff leve do canal de string da Microsoft. Os **benchmarks de controle** medem a eficiência subjacente sem sobrecarga de fila/sink:

| Benchmark de Controle | N=1 | N=10 | N=100 |
|---|---|---|---|
| **TimestampNowOnly** | 4,15&times; | 5,05&times; | 4,54&times; |
| **LogEntryAllocateOnly** | 8,48&times; | 23,48&times; | 20,11&times; |

**Binário AOT: 3.020 KB**

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

---

## PicoDI

Contêiner de DI sem reflexão com geração de código em tempo de compilação.

### Registro

Todos os métodos `Register*` retornam `ISvcContainer` para encadeamento fluente.

```csharp
var container = new SvcContainer();

// Baseado em fábrica (sempre funciona, não requer source gen)
container.RegisterSingleton<IService>(scope => new Service(scope.GetService<IDep>()));
container.RegisterScoped<IRepository>(scope => new Repository());
container.RegisterTransient<IValidator>(scope => new Validator());

// Instância pré-construída
container.RegisterSingle<IClock>(SystemClock.Instance);

// Genéricos abertos
container.Register(typeof(IRepository<>), typeof(SqlRepository<>), SvcLifetime.Scoped);

// Baseado em tipo (requer o source generator PicoDI.Gen)
container.RegisterSingleton<IService, Service>();

// Serviços hospedados
container.RegisterHostedSvc<BackgroundWorker>();

container.Build();  // Congela os registros
```

### Resolução

```csharp
using var scope = container.CreateScope();

// Resolução tipada (sem lookup via métodos Resolve.* gerados)
var svc = scope.GetService<IService>();
var repos = scope.GetServices<IRepository>();

// Resolução baseada em tipo
var instance = scope.GetService(typeof(IService));
```

### Ciclos de Vida

| Ciclo de Vida | Instanciação | Descarte |
|---|---|---|
| **Transient** | Nova a cada requisição | Rastreado pelo escopo de resolução, descartado em ordem LIFO |
| **Scoped** | Uma vez por escopo | Descartado quando o escopo é descartado |
| **Singleton** | Uma vez por contêiner | Descartado quando o contêiner é descartado |

### Source Generator (PicoDI.Gen)

Adicione `PicoDI.Gen` como um analyzer para habilitar registros baseados em tipo em tempo de compilação:

```xml
<PackageReference Include="PicoDI.Gen" PrivateAssets="all" />
```

O generator varre todas as chamadas `Register*` e emite:

- **Método de extensão `ConfigureGeneratedServices()`** com delegates de fábrica inline (sem reflexão)
- **Métodos `Resolve.*` tipados** para caminhos de resolução sem lookup
- **Detecção de dependência circular em tempo de compilação**
- **Metadados de genéricos abertos** para descoberta entre assemblies
- **Auto-configurador `[ModuleInitializer]`** que registra com `SvcContainerAutoConfiguration`

**Antes** (seu código):
```csharp
var container = new SvcContainer();
container.RegisterSingleton<IService, Service>();
container.Build();
```

**Depois** (gerado):
```csharp
// Gerado automaticamente por PicoDI.Gen
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

Construtor fluente opcional para serviços hospedados:

```csharp
using var hostBuilder = new SvcHostBuilder();
var host = await hostBuilder
    .ConfigureServices(container =>
    {
        container.RegisterSingleton<IService>(_ => new Service());
        container.RegisterHostedSvc<Worker>();
    })
    .BuildAsync();

// A aplicação executa até ser interrompida
await host.StopAsync();
```

---

## PicoCfg

Gerenciamento de configuração assíncrono com modelo de provedores.

### Modelo de Provedores

```
Fontes ──→ Provedores ──→ Raiz ──→ Consumidor
```

As fontes definem **como** a configuração é produzida. Os provedores são as instâncias materializadas. A raiz compõe os snapshots dos provedores em uma visão unificada. Os consumidores consultam via `TryGetValue` ou `GetValue`.

### Fontes Embutidas

| Fonte | Descrição |
|---|---|
| **Dictionary** | Pares chave-valor em memória |
| **Environment Variables** | Variáveis de ambiente do SO, filtragem por prefixo, mapeamento `__` &rarr; `:` |
| **Command Line** | `--chave=valor`, `--chave valor`, `-chave valor`, `/chave valor` |
| **Stream** | Parsing de texto linha a linha no formato `chave=valor` com monitoramento de arquivo |
| **File Watching** | Recarga automática ao alterar arquivo com debounce |
| **Chained** | Fallback para outra instância de `ICfg` |
| **KeyPerFile** | Estilo Kubernetes ConfigMap &mdash; nome-do-arquivo=chave, conteúdo=valor |

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

### Binding com Source Generator

Adicione `PicoCfg.Gen` como um analyzer. O source generator descobre alvos de binding em tempo de compilação a partir de pontos de chamada `CfgBind.Bind<T>` / `CfgBind.TryBind<T>` / `CfgBind.BindInto<T>` &mdash; nenhum atributo é necessário. O código gerado registra binders fortemente tipados que resolvem valores de configuração por caminho de chave sem reflexão em runtime.

```csharp
using PicoCfg;

public sealed class AppSettings
{
    public string Name { get; init; }
    public int MaxRetries { get; init; } = 3;
    public bool EnableFeature { get; init; }
}

// O ponto de chamada dispara a geração de código para AppSettings
var settings = CfgBind.Bind<AppSettings>(cfg, "App");
```

---

## PicoLog

Registro estruturado de logs com modelos de mensagem em tempo de compilação.

### Níveis de Log

| Nível | Valor | Uso |
|---|---|---|
| **Emergency** | 0 | O sistema está inutilizável |
| **Alert** | 1 | Ação deve ser tomada imediatamente |
| **Critical** | 2 | Condições críticas |
| **Error** | 3 | Condições de erro |
| **Warning** | 4 | Condições de aviso |
| **Notice** | 5 | Normal, mas significativo |
| **Info** | 6 | Mensagens informativas |
| **Debug** | 7 | Mensagens de depuração |
| **Trace** | 8 | Rastreamento diagnóstico detalhado |
| **None** | 255 | Desativa todos os logs |

### Modelos de Mensagem

Suporte tanto para interpolação de strings quanto para `FormattableString` com parâmetros nomeados para logging estruturado:

```csharp
logger.Info($"Processing order {orderId} for {customer}");
logger.Log(LogLevel.Info, $"User {user} logged in from {ipAddress}");

// Logging estruturado com propriedades explícitas
logger.LogStructured(LogLevel.Error, "Payment failed",
    properties: [new("OrderId", orderId), new("Amount", amount)],
    exception: ex);
```

### Mensagens com Source Generator

O atributo `[PicoLogMessage]` em métodos estáticos parciais emite métodos de extensão fortemente tipados e compatíveis com AOT:

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

### Sinks Embutidos

```csharp
var sinks = new List<ILogSink>
{
    new ConsoleSink(new ConsoleFormatter()),          // Console simples
    new ColoredConsoleSink(new ConsoleFormatter()),    // Colorido por nível
    new FileSink(new ConsoleFormatter(),               // Saída em lote para arquivo
        new FileSinkOptions { FilePath = "app.log", BatchSize = 100 })
};
using var factory = new LoggerFactory(sinks);
```

### Sinks Personalizados

```csharp
public sealed class CustomSink : ILogSink
{
    public Task WriteAsync(LogEntry entry, CancellationToken ct = default)
    {
        // Escreva no seu backend
        return Task.CompletedTask;
    }

    public void Dispose() { }
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
```

---

## Integração de Módulos

Quando `PicoDI`, `PicoCfg.DI` e `PicoLog.DI` são todos referenciados, a conexão acontece automaticamente:

1. Cada módulo tem seu source generator que registra um configurador baseado em `ModuleInitializer`
2. `new SvcContainer()` executa todos os configuradores via `SvcContainerAutoConfiguration.TryApplyConfiguration`
3. Nenhuma conexão manual necessária

### APIs de Integração com DI

**PicoCfg.DI** &mdash; extensões em `ISvcContainer`:
- `RegisterCfgRoot(ICfgRoot root)` &mdash; registra `ICfgRoot` e `ICfg`
- `RegisterCfgTransient/Scoped/Singleton<T>(string? section)` &mdash; vincula POCOs a partir da configuração
- `RegisterCfgOptionsSingleton/Scoped<T>(string? section)` &mdash; suporte a options tipados

**PicoLog.DI** &mdash; extensão em `ISvcContainer`:
- `AddPicoLog(Action<LoggingOptions> configure)` &mdash; registra `ILoggerFactory` e `ILogger<>`
- `WriteTo.Console()` / `.ColoredConsole()` / `.File(caminho)` / `.Sink(personalizado)` &mdash; configuração de sinks
- `ReadFrom.RegisteredSinks()` &mdash; inclui instâncias `ILogSink` registradas no DI

### Exemplo Mínimo Combinado

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

## Filosofia de Design

**克制 (Restraint) / Contenção** — Apenas DI, Config e Logging. Nada de Web framework, ORM ou fila de mensagens. Se não é infraestrutura universal, não pertence aqui. PicoHex é o denominador comum que toda aplicação precisa &mdash; nada mais, nada menos.

**专注 (Focus) / Foco** — Cada módulo faz uma coisa. PicoDI é um contêiner, não um service locator. PicoCfg é configuração, não um sistema de feature flags. PicoLog é logging, não um pipeline de telemetria. Especialização profunda sobre generalidade superficial.

**优雅 (Elegance) / Elegância** — APIs são mínimas. Source generators fazem a conexão em tempo de compilação. O desenvolvedor escreve código direto; as ferramentas cuidam da complexidade. `new SvcContainer()` substitui 100+ linhas de cerimônia do `Host.CreateDefaultBuilder()`.

**高效 (Efficiency) / Eficiência** — AOT First não é uma reflexão tardia &mdash; é a fundação. Zero reflexão. Zero sobrecarga em runtime. Tudo que pode ser resolvido em tempo de compilação é resolvido em tempo de compilação. Binários mínimos, cold starts rápidos, performance previsível.

---

## Casos de Uso

| Cenário | PicoDI | PicoCfg | PicoLog | Por quê |
|---|---|---|---|---|
| **Ferramentas CLI** | Opcional | Essencial | Essencial | Parse de argumentos/config, controle de verbosidade. DI está disponível se a ferramenta crescer. |
| **Serverless / Lambda** | Essencial | Essencial | Essencial | Cold start é o gargalo. DI compilado em AOT com caminhos de resolução pré-gerados. |
| **WASM / Blazor** | Essencial | Essencial | Essencial | Tamanho de download importa. 11 pacotes, trim-friendly, sem inchaço em runtime. |
| **Embarcados / IoT** | Quando necessário | Essencial | Essencial | Dispositivos com recursos limitados. Binário mínimo, zero alocações quando possível. |

---

## Pacotes

| Pacote | Descrição | NuGet |
|---|---|---|
| **PicoDI** | Contêiner DI sem reflexão com geração de código em tempo de compilação | [![NuGet](https://img.shields.io/nuget/v/PicoDI)](https://nuget.org/packages/PicoDI) |
| **PicoDI.Abs** | Abstrações para PicoDI (`ISvcContainer`, `ISvcScope`, `SvcDescriptor`) | [![NuGet](https://img.shields.io/nuget/v/PicoDI.Abs)](https://nuget.org/packages/PicoDI.Abs) |
| **PicoDI.Gen** | Source generator Roslyn &mdash; registro e resolução em tempo de compilação | [![NuGet](https://img.shields.io/nuget/v/PicoDI.Gen)](https://nuget.org/packages/PicoDI.Gen) |
| **PicoCfg** | Gerenciamento de configuração assíncrono com modelo de provedores | [![NuGet](https://img.shields.io/nuget/v/PicoCfg)](https://nuget.org/packages/PicoCfg) |
| **PicoCfg.Abs** | Abstrações para PicoCfg (`ICfg`, `ICfgRoot`, `ICfgSection`) | [![NuGet](https://img.shields.io/nuget/v/PicoCfg.Abs)](https://nuget.org/packages/PicoCfg.Abs) |
| **PicoCfg.Gen** | Source generator para binding tipado de configuração (`CfgBind.Bind<T>`) | [![NuGet](https://img.shields.io/nuget/v/PicoCfg.Gen)](https://nuget.org/packages/PicoCfg.Gen) |
| **PicoCfg.DI** | Integração PicoDI para PicoCfg (`RegisterCfgRoot`, `RegisterCfgSingleton`) | [![NuGet](https://img.shields.io/nuget/v/PicoCfg.DI)](https://nuget.org/packages/PicoCfg.DI) |
| **PicoLog** | Logging estruturado com modelos de mensagem em tempo de compilação | [![NuGet](https://img.shields.io/nuget/v/PicoLog)](https://nuget.org/packages/PicoLog) |
| **PicoLog.Abs** | Abstrações para PicoLog (`ILogger`, `ILogSink`, `LogLevel`) | [![NuGet](https://img.shields.io/nuget/v/PicoLog.Abs)](https://nuget.org/packages/PicoLog.Abs) |
| **PicoLog.Gen** | Source generator para métodos `[PicoLogMessage]` | [![NuGet](https://img.shields.io/nuget/v/PicoLog.Gen)](https://nuget.org/packages/PicoLog.Gen) |
| **PicoLog.DI** | Integração PicoDI para PicoLog (`AddPicoLog`, `WriteTo`, `ReadFrom`) | [![NuGet](https://img.shields.io/nuget/v/PicoLog.DI)](https://nuget.org/packages/PicoLog.DI) |

---

## Comparação

### vs Autofac

Autofac é um contêiner DI maduro e rico em funcionalidades, com injeção de propriedade, decorators, interceptors e módulos. PicoDI adota a abordagem oposta: **zero reflexão em runtime, registro apenas em tempo de compilação, seguro para AOT por design**. Se você precisa de flexibilidade em runtime, use Autofac. Se você precisa de **AOT e sobrecarga mínima**, use PicoDI.

### vs Lamar

Lamar é um contêiner DI performático que usa geração de código em runtime (`DynamicAssembly` + emissão de IL). PicoDI usa source generators do Roslyn para geração de código em **tempo de compilação**. Lamar suporta mais funcionalidades (interceptação, decoration); PicoDI é mínimo e AOT-first.

### vs Serilog

Serilog é o padrão ouro para logging estruturado em .NET, com um vasto ecossistema de sinks. PicoLog não é uma substituição para o Serilog &mdash; é uma **alternativa leve** para projetos que valorizam compatibilidade com AOT e dependências mínimas acima da variedade de sinks. O source generator do PicoLog para modelos de mensagem oferece qualidade de logging estruturado comparável.

### vs Microsoft.Extensions

PicoHex não é uma extensão do `Microsoft.Extensions` &mdash; é uma **alternativa**. Projetada desde o início para Native AOT, com geração de código em tempo de compilação em vez de reflexão em runtime. Se você está construindo aplicações ASP.NET tradicionais, mantenha-se com Microsoft.Extensions. Se você está construindo **ferramentas CLI, funções serverless, apps WASM ou sistemas embarcados**, PicoHex foi feito para o seu caso de uso.

---

## Licença &amp; Contribuição

Licença MIT. Repositório: [https://github.com/PicoHex/PicoHex](https://github.com/PicoHex/PicoHex)
