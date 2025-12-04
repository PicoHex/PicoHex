# PicoHex

English | [简体中文](README.zh-CN.md)

---

A fully asynchronous edge computing MVP (Minimum Viable Product) built on AOT (Ahead-of-Time) compilation. Implements core functionality **without dependencies** on external libraries for DI containers, logging frameworks, configuration systems, web servers, or similar components.

## Overview

PicoHex is a lightweight, high-performance framework designed for edge computing and IoT scenarios. The project aims to provide a completely self-contained foundation for building applications in resource-constrained environments, with full support for AOT compilation.

### Key Features

- 🚀 **Full AOT Support**: All components are AOT-compatible for faster startup and better performance
- 📦 **Zero External Dependencies**: Self-contained implementation of all core functionality
- ⚡ **High Performance**: Asynchronous architecture for high throughput
- 🔧 **Modular Design**: Use only what you need
- 🌐 **Protocol Support**: HTTP, CoAP, MQTT implementations
- 💉 **Dependency Injection**: Full-featured DI container with source generation

## Core Components

### Pico.DI - Dependency Injection Container

AOT-compatible IoC container supporting **Transient/Scoped/Singleton** lifetimes with circular dependency detection. Includes a source generator for compile-time optimization.

**Features:**
- Constructor injection
- Circular dependency detection
- Source generator for AOT optimization
- Support for IEnumerable<T> injection
- Compile-time error detection

**Example:**
```csharp
var container = Bootstrap.CreateContainer();
container
    .RegisterTransient<IUserService, UserService>()
    .RegisterScoped<IDataService, DataService>()
    .RegisterSingleton<IConfigService, ConfigService>();

var provider = container.GetProvider();
var userService = provider.Resolve<IUserService>();
```

### Pico.Cfg - Configuration Framework

Stream-based configuration framework.

**Features:**
- Lightweight configuration management
- Multiple configuration sources support
- Stream-based processing

### Pico.Logger - Logging Framework

Logging framework with **console and file sinks**.

**Features:**
- Console output for real-time logging
- File output for persistent logs
- Structured logging
- High-performance async logging

### Pico.Node - Network Nodes

TCP and UDP network communication support:

**Features:**
- TcpNode for TCP server implementation
- UdpNode for UDP server implementation
- Asynchronous network communication
- Configurable connection pools
- Custom handler support

**Example:**
```csharp
var svcRegistry = Bootstrap.CreateContainer();
svcRegistry
    .RegisterTransient<ITcpHandler, HttpHandler>()
    .RegisterSingle<TcpNode>(sp => new TcpNode(
        new TcpNodeOptions
        {
            Endpoint = new IPEndPoint(IPAddress.Any, 8080),
            HandlerFactory = sp.Resolve<ITcpHandler>,
            Logger = sp.Resolve<ILogger<TcpNode>>(),
            MaxConnections = 500
        }
    ));
```

### Pico.Proto - Protocol Implementations

Multiple IoT and edge computing protocols:

#### HTTP
- HTTP request/response parsing
- HTTP server implementation
- RESTful API support

#### CoAP (Constrained Application Protocol)
- Lightweight IoT protocol
- Designed for resource-constrained devices
- Request/response model support

#### MQTT
- Message Queue Telemetry Transport
- Publish/subscribe pattern
- IoT device communication

### Pico.Html - HTML Processing

HTML generation and processing capabilities:
- HTML generation
- Template engine
- Server-side rendering support

### Pico.Trsp - Transport Layer

Low-level transport abstraction providing unified transport interfaces for upper-layer protocols.

## Project Structure

```
PicoHex/
├── src/                          # Source code
│   ├── Pico.Core/               # Core components
│   ├── Configuration/           # Configuration framework
│   ├── DependencyInjection/     # DI container and source generator
│   ├── Logger/                  # Logging framework
│   ├── Node/                    # Network nodes
│   ├── Proto/                   # Protocol implementations
│   ├── Html/                    # HTML processing
│   └── Transport/               # Transport layer
├── samples/                     # Sample projects
│   ├── Pico.DI.Sample/         # DI examples
│   ├── Pico.DI.Aot.Sample/     # AOT DI examples
│   ├── Pico.Node.Http.Sample/  # HTTP server example
│   └── ...
└── tests/                       # Test projects
```

## Getting Started

### Prerequisites

- .NET 8.0 or later
- C# 12

### Quick Start

1. Clone the repository:
```bash
git clone https://github.com/PicoHex/PicoHex.git
cd PicoHex
```

2. Build the solution:
```bash
dotnet build
```

3. Run a sample:
```bash
cd samples/Pico.Node.Http.Sample
dotnet run
```

## Use Cases

### Edge Computing
- IoT gateway devices
- Edge data processing nodes
- Real-time data collection and processing

### Internet of Things
- Device-to-device communication (MQTT, CoAP)
- Sensor data collection
- Smart home gateways

### Microservices
- Lightweight HTTP API services
- Fast-starting containerized applications
- Services in resource-constrained environments

### Mobile Applications
- iOS/Android native apps
- Cross-platform apps requiring AOT
- Performance-sensitive mobile backends

## Performance Characteristics

1. **Fast Startup**: AOT compilation reduces startup time
2. **Low Memory Footprint**: No reflection or dynamic compilation overhead
3. **High Throughput**: Asynchronous architecture for high concurrency
4. **Small Size**: No external dependencies, minimal deployment size

## Development Status

The project is currently in MVP (Minimum Viable Product) stage with core features implemented:
- ✅ Dependency injection container (with AOT support)
- ✅ Logging framework
- ✅ Configuration framework
- ✅ TCP/UDP network communication
- ✅ HTTP protocol support
- ✅ CoAP protocol support
- ✅ MQTT protocol support
- ✅ HTML processing

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## Contributing

Contributions are welcome! Please feel free to submit issues and pull requests.

## Technology Stack

- **.NET 8+**: Using the latest .NET technology
- **C# 12**: Leveraging the latest language features
- **Source Generators**: Compile-time code generation
- **Native AOT**: Native ahead-of-time compilation
