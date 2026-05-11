# PicoHex - Lightweight .NET Libraries

A collection of small, AOT-compatible .NET libraries designed for modern
cross-platform applications. Each library works standalone or together.

| Package | Description |
|---------|-------------|
| PicoDI  | Zero-reflection DI container with compile-time source generation |
| PicoCfg | Async-first configuration management with source-generated bindings |
| PicoLog | Structured logging with compile-time message templates and DI integration |

## Quick Start

### PicoDI

```shell
dotnet add package PicoDI
```

```csharp
// Define a service
public interface IGreeter
{
    string Greet(string name);
}

public class Greeter : IGreeter
{
    public string Greet(string name) => $"Hello, {name}!";
}

// Register and resolve
var container = SvcContainer.Create();
container.AddTransient<IGreeter, Greeter>();

var greeter = container.Resolve<IGreeter>();
Console.WriteLine(greeter.Greet("World"));
```

### PicoCfg

```shell
dotnet add package PicoCfg
```

```csharp
// Load configuration from sources
var cfg = await CfgRoot.CreateAsync();

// Access values
var value = await cfg.GetSection("MyApp:Setting").GetStringAsync();
```

### PicoLog

```shell
dotnet add package PicoLog
```

```csharp
using ILogger logger = /* create logger */;
logger.LogInfo("Application started with {Args}", args);
```

## Packages

| Package | NuGet |
|---------|-------|
| PicoDI | [![NuGet](https://img.shields.io/nuget/v/PicoDI)](https://nuget.org/packages/PicoDI) |
| PicoDI.Abs | [![NuGet](https://img.shields.io/nuget/v/PicoDI.Abs)](https://nuget.org/packages/PicoDI.Abs) |
| PicoDI.Gen | [![NuGet](https://img.shields.io/nuget/v/PicoDI.Gen)](https://nuget.org/packages/PicoDI.Gen) |
| PicoCfg | [![NuGet](https://img.shields.io/nuget/v/PicoCfg)](https://nuget.org/packages/PicoCfg) |
| PicoCfg.Abs | [![NuGet](https://img.shields.io/nuget/v/PicoCfg.Abs)](https://nuget.org/packages/PicoCfg.Abs) |
| PicoCfg.Gen | [![NuGet](https://img.shields.io/nuget/v/PicoCfg.Gen)](https://nuget.org/packages/PicoCfg.Gen) |
| PicoCfg.DI | [![NuGet](https://img.shields.io/nuget/v/PicoCfg.DI)](https://nuget.org/packages/PicoCfg.DI) |
| PicoLog | [![NuGet](https://img.shields.io/nuget/v/PicoLog)](https://nuget.org/packages/PicoLog) |
| PicoLog.Abs | [![NuGet](https://img.shields.io/nuget/v/PicoLog.Abs)](https://nuget.org/packages/PicoLog.Abs) |
| PicoLog.Gen | [![NuGet](https://img.shields.io/nuget/v/PicoLog.Gen)](https://nuget.org/packages/PicoLog.Gen) |
| PicoLog.DI | [![NuGet](https://img.shields.io/nuget/v/PicoLog.DI)](https://nuget.org/packages/PicoLog.DI) |

## License

This project is licensed under the [MIT License](LICENSE).
