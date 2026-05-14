# PicoLog

High-performance structured logging for .NET Native AOT.

## Quick Start

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

## Log Levels

| Level | Value | Usage |
|---|---|---|
| **Emergency** | 0 | System is unusable |
| **Alert** | 1 | Action must be taken immediately |
| **Critical** | 2 | Critical conditions |
| **Error** | 3 | Error conditions |
| **Warning** | 4 | Warning conditions |
| **Notice** | 5 | Normal but significant |
| **Info** | 6 | Informational messages |
| **Debug** | 7 | Debug-level messages |
| **Trace** | 8 | Detailed diagnostic tracing |
| **None** | 255 | Disables all logging |

## Message Templates

```csharp
logger.Info($"Processing order {orderId} for {customer}");

// Structured logging with explicit properties
logger.LogStructured(LogLevel.Error, "Payment failed",
    properties: [new("OrderId", orderId), new("Amount", amount)],
    exception: ex);
```

## Source-Generated Messages

```csharp
public static partial class AppLogs
{
    [PicoLogMessage(LogLevel.Info, EventId = 1001, Message = "Order {OrderId} placed")]
    public static partial void OrderPlaced(this ILogger logger, string orderId);
}

logger.OrderPlaced("ORD-12345");
```

## Built-in Sinks

```csharp
var sinks = new List<ILogSink>
{
    new ConsoleSink(new ConsoleFormatter()),
    new ColoredConsoleSink(new ConsoleFormatter()),
    new FileSink(new ConsoleFormatter(),
        new FileSinkOptions { FilePath = "app.log", BatchSize = 100 })
};
```

## Custom Sinks

```csharp
public sealed class CustomSink : ILogSink
{
    public Task WriteAsync(LogEntry entry, CancellationToken ct = default)
    {
        // Write to your backend
        return Task.CompletedTask;
    }
    public void Dispose() { }
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
```

## DI Integration (PicoLog.DI)

```csharp
container.AddPicoLog(o =>
{
    o.MinLevel = LogLevel.Info;
    o.WriteTo.ColoredConsole();
    o.WriteTo.File("app.log");
});
var logger = container.CreateScope().GetService<ILogger<Program>>();
```

## Packages

| Package | Description |
|---|---|
| **PicoLog** | Logging runtime |
| **PicoLog.Abs** | `ILogger`, `ILogSink`, `LogLevel`, `LogEntry` |
| **PicoLog.Gen** | `[PicoLogMessage]` source generator |
| **PicoLog.DI** | DI integration |

[← Back to PicoHex](../README.md)
