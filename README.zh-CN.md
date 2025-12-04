# PicoHex

[English](README.md) | 简体中文

---

## 项目简介

PicoHex 是一个完全异步的边缘计算 MVP（最小可行产品），基于 AOT（提前编译）构建。该项目的核心特点是**无需依赖**任何外部库，包括 DI 容器、日志框架、配置系统、Web 服务器等组件，所有核心功能都是自主实现的。

## 项目目标

PicoHex 旨在为边缘计算场景提供一个轻量级、高性能、AOT 友好的基础框架，特别适合以下场景：
- IoT 设备和边缘节点
- 资源受限的嵌入式系统
- 需要快速启动的微服务
- 需要 AOT 编译支持的移动应用（iOS/Android）

## 核心组件

### 1. Pico.DI - 依赖注入容器

一个完全兼容 AOT 编译的 IoC（控制反转）容器，支持：
- **三种生命周期**：Transient（瞬态）/ Scoped（作用域）/ Singleton（单例）
- **循环依赖检测**：编译时和运行时检测循环依赖
- **源代码生成器**：通过源代码生成器实现编译时优化
- **零反射**：在 AOT 环境中避免使用反射，提升性能

**特性：**
- 自动服务发现和注册
- 构造函数自动注入
- 支持 IEnumerable<T> 批量注入
- 编译时错误检测

### 2. Pico.Cfg - 配置框架

基于流的配置框架，提供：
- 轻量级配置管理
- 支持多种配置源
- 流式处理配置数据
- 无需第三方配置库

### 3. Pico.Logger - 日志框架

自研的日志框架，支持：
- **控制台输出**：实时日志输出到控制台
- **文件输出**：日志持久化到文件
- 结构化日志
- 高性能异步日志

### 4. Pico.Node - 网络节点

提供 TCP 和 UDP 网络通信能力：
- **TcpNode**：TCP 服务器实现
- **UdpNode**：UDP 服务器实现
- 异步网络通信
- 可配置的连接池和积压队列
- 支持自定义处理器

### 5. Pico.Proto - 协议实现

实现多种物联网和边缘计算常用协议：

#### HTTP
- HTTP 请求/响应解析
- HTTP 服务器实现
- 支持 RESTful API

#### CoAP (Constrained Application Protocol)
- 轻量级物联网协议
- 适用于资源受限设备
- 支持请求/响应模型

#### MQTT
- 消息队列遥测传输协议
- 发布/订阅模式
- 适用于 IoT 设备通信

### 6. Pico.Html - HTML 处理

提供 HTML 相关功能：
- HTML 生成和处理
- 模板引擎
- 支持服务端渲染

### 7. Pico.Trsp - 传输层

底层传输层抽象，为上层协议提供统一的传输接口。

## 技术亮点

### AOT 编译支持

PicoHex 的所有组件都完全支持 AOT 编译：
- **无反射**：避免运行时反射，提升性能
- **更快的启动**：预编译代码，减少启动时间
- **更小的体积**：编译时优化，减少程序大小
- **更好的性能**：编译时优化比运行时更高效

### 源代码生成器

Pico.DI.SourceGen 是一个编译时代码生成器：
- 在编译时分析项目中的服务
- 自动生成服务注册代码
- 生成 AOT 友好的工厂方法
- 编译时检测循环依赖
- 提供详细的诊断信息

### 零外部依赖

PicoHex 不依赖任何第三方库：
- 完全自主实现所有核心功能
- 减少依赖冲突
- 更好的安全性和可控性
- 更小的部署体积

## 项目结构

```
PicoHex/
├── src/                          # 源代码目录
│   ├── Pico.Core/               # 核心组件
│   ├── Configuration/           # 配置相关
│   │   ├── Pico.Cfg.Abs/       # 配置抽象接口
│   │   └── Pico.Cfg/           # 配置实现
│   ├── DependencyInjection/     # 依赖注入
│   │   ├── Pico.DI.Abs/        # DI 抽象接口
│   │   ├── Pico.DI/            # DI 实现
│   │   └── Pico.DI.SourceGen/  # 源代码生成器
│   ├── Logger/                  # 日志
│   │   ├── Pico.Logger.Abs/    # 日志抽象接口
│   │   └── Pico.Logger/        # 日志实现
│   ├── Node/                    # 网络节点
│   │   ├── Pico.Node.Core/     # 节点核心
│   │   ├── Pico.Node/          # 节点实现
│   │   └── Pico.Node.Http/     # HTTP 节点
│   ├── Proto/                   # 协议实现
│   │   ├── HTTP/               # HTTP 协议
│   │   ├── CoAP/               # CoAP 协议
│   │   └── MQTT/               # MQTT 协议
│   ├── Html/                    # HTML 处理
│   └── Transport/               # 传输层
│       └── Pico.Trsp/          # 传输实现
├── samples/                     # 示例项目
│   ├── Pico.DI.Sample/         # DI 使用示例
│   ├── Pico.DI.Aot.Sample/     # AOT DI 示例
│   ├── Pico.Cfg.Sample/        # 配置示例
│   ├── Pico.Logger.Sample/     # 日志示例
│   ├── Pico.Node.Sample/       # 节点示例
│   ├── Pico.Node.Http.Sample/  # HTTP 服务器示例
│   └── Pico.Html.Sample/       # HTML 示例
└── tests/                       # 测试项目
    ├── Pico.DI.Test/           # DI 单元测试
    └── Pico.Node.Test/         # 节点单元测试
```

## 快速开始

### 1. 依赖注入示例

```csharp
// 创建 IoC 容器
var container = Bootstrap.CreateContainer();

// 注册服务
container
    .RegisterTransient<IUserService, UserService>()
    .RegisterScoped<IDataService, DataService>()
    .RegisterSingleton<IConfigService, ConfigService>();

// 获取服务提供者
var provider = container.GetProvider();

// 解析服务
var userService = provider.Resolve<IUserService>();
```

### 2. AOT 依赖注入示例

```csharp
// 创建 AOT 优化的容器
var container = AotBootstrap.CreateAotContainer();

// 注册服务（将由源代码生成器优化）
container
    .RegisterTransient<IUserService, UserService>()
    .RegisterTransient<IEmailService, EmailService>();

var provider = container.GetProvider();
var userService = provider.Resolve<IUserService>();
```

### 3. HTTP 服务器示例

```csharp
// 创建容器
var svcRegistry = Bootstrap.CreateContainer();

// 注册 TCP 处理器和节点
svcRegistry
    .RegisterTransient<ITcpHandler, HttpHandler>()
    .RegisterSingle<TcpNode>(sp => new TcpNode(
        new TcpNodeOptions
        {
            Endpoint = new IPEndPoint(IPAddress.Any, 8080),
            HandlerFactory = sp.Resolve<ITcpHandler>,
            Logger = sp.Resolve<ILogger<TcpNode>>(),
            MaxConnections = 500,
            BacklogSize = 50
        }
    ));

// 注册日志
svcRegistry.RegisterLogger();

// 启动服务器
var svcProvider = svcRegistry.GetProvider();
var tcpServer = svcProvider.Resolve<TcpNode>();

var cts = new CancellationTokenSource();
await tcpServer.StartAsync(cts.Token);
```

## 应用场景

### 边缘计算
- IoT 网关设备
- 边缘数据处理节点
- 实时数据采集和处理

### 物联网
- 设备间通信（MQTT、CoAP）
- 传感器数据采集
- 智能家居网关

### 微服务
- 轻量级 HTTP API 服务
- 快速启动的容器化应用
- 资源受限环境的服务

### 移动应用
- iOS/Android 原生应用
- 需要 AOT 编译的跨平台应用
- 性能敏感的移动后端

## 性能特点

1. **快速启动**：AOT 编译减少启动时间
2. **低内存占用**：无需反射和动态编译，减少内存开销
3. **高吞吐量**：异步架构，支持高并发
4. **小体积**：无外部依赖，最小化部署体积

## 开发状态

该项目目前处于 MVP（最小可行产品）阶段，核心功能已实现：
- ✅ 依赖注入容器 (含 AOT 支持)
- ✅ 日志框架
- ✅ 配置框架
- ✅ TCP/UDP 网络通信
- ✅ HTTP 协议支持
- ✅ CoAP 协议支持
- ✅ MQTT 协议支持
- ✅ HTML 处理

## 许可证

本项目采用 MIT 许可证。详见 [LICENSE](LICENSE) 文件。

## 贡献

欢迎提交问题和拉取请求！

## 技术栈

- **.NET 8+**：使用最新的 .NET 技术
- **C# 12**：利用最新的语言特性
- **Source Generators**：编译时代码生成
- **Native AOT**：原生提前编译

## 总结

PicoHex 是一个为边缘计算和 IoT 场景设计的轻量级、高性能、完全自主的 .NET 框架。通过避免外部依赖和充分利用 AOT 编译，它为资源受限环境提供了最佳的性能和部署体验。
