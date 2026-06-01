# PicoMediator Design

**Status:** Draft  
**Module:** PicoMediator (planned)  
**Depends On:** PicoDI, PicoAop (optional)

---

## 1. Philosophy

PicoMediator 的设计方法论来源于 ZeroMQ 的核心理念：**交互模式是原子原语，由类型系统编码，模式不可混用**。

ZeroMQ 提取出了 REQ/REP、PUB/SUB、PUSH/PULL 等不可再分的交互原语。每个 socket 类型决定了你能做什么操作——REQ 只能 send+recv，PUB 只能 send。这些约束不是运行时检查，而是 API 层面的保证。

PicoMediator 将同一范式应用于 C# 类型系统：

> **协议约束由泛型和接口编码。依赖 `ISender` 的类不可能意外调用 `Publish`。`IRequest<T>` 必须有返回值。`INotification` 永远没有返回值。**

### 核心设计原则

| 原则 | 说明 |
|------|------|
| **模式即原语** | 每种交互模式是独立的，不可再分 |
| **类型系统编码协议** | 泛型约束 = ZeroMQ 的 socket 类型 |
| **中间层独立** | ROUTER = PicoAop。不是 Mediator 的 "pipeline 功能" |
| **克制** | 不实现仅有边缘场景的模式。已有原语可组合 |


## 2. Interaction Patterns

### 2.1 REQ/REP — Send（请求-响应）

```
ZeroMQ:    REQ ────request───→ REP
           REQ ←──response─── REP

PicoMed:   ISender.Send(request) → Handler → ValueTask<TResponse>
```

**协议约束：**
- 1:1 — 一个请求类型只能对应一个 Handler
- 必须有响应 — `IRequest<TResponse>` 强制声明响应类型
- 发送方等待响应 — `ValueTask<T>` 语义
- 无订阅者时 — 编译期/运行时报错（不是静默丢弃）

**类型编码：**

```csharp
public interface IRequest<TResponse> { }

public interface IRequestHandler<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    ValueTask<TResponse> Handle(TRequest request, CancellationToken ct = default);
}
```

`TResponse` 可以是任何类型：
- `OrderResult` — 单值响应
- `ChatResponse(IAsyncEnumerable<string>)` — 自然流式（不需要 IStreamRequest）
- `VoidResult` — 无返回值命令（不需要 Unit，不需要 ICommand）

### 2.2 PUB/SUB — Publish（发布-订阅）

```
ZeroMQ:    PUB ────msg→ SUB₁
                  └──→ SUB₂
                  └──→ SUB₃

PicoMed:   IPublisher.Publish(notification)
                → Handler₁.Handle()
                → Handler₂.Handle()
                → Handler₃.Handle()
```

**协议约束：**
- 1:N — 多个 Handler 可以订阅同一通知类型
- 无返回值 — `INotification` 没有泛型参数
- 发布者不知道订阅者是谁
- 订阅者之间互相不知道对方
- 无订阅者时 — 静默丢弃（ZeroMQ PUB 语义）
- 流式不可用 — Publish 没有返回值，无法 `await foreach`

**类型编码：**

```csharp
public interface INotification { }

public interface INotificationHandler<TNotification>
    where TNotification : INotification
{
    ValueTask Handle(TNotification notification, CancellationToken ct = default);
}
```

`Handle` 返回非泛型 `ValueTask` — 协议层面禁止回信。

### 2.3 PUSH/PULL — 不在 v1

PUSH/PULL 是 ZeroMQ 的公平分发模式（谁有空谁处理）。在进程内没有 "空闲 Worker" 的概念 — 需要外部队列才有实际意义。不进入 `PicoMediator.Abs`。


## 3. Interface Design

### 3.1 Caller Ports（窄接口）

ZeroMQ 的 socket 类型决定可用操作。PicoMediator 用接口隔离达到同一效果：

```csharp
/// <summary>REQ socket — 只能 Send，不能 Publish。</summary>
public interface ISender
{
    ValueTask<TResponse> Send<TRequest, TResponse>(
        TRequest request, CancellationToken ct = default)
        where TRequest : IRequest<TResponse>;
}

/// <summary>PUB socket — 只能 Publish，不能 Send。</summary>
public interface IPublisher
{
    ValueTask Publish<TNotification>(
        TNotification notification, CancellationToken ct = default)
        where TNotification : INotification;
}

/// <summary>组合 socket — REQ + PUB。仅编排层使用。</summary>
public interface IMediator : ISender, IPublisher { }
```

| 依赖方 | 应依赖 | 理由 |
|--------|--------|------|
| 业务组件（只发请求） | `ISender` | 不应意外调 `Publish` |
| 业务组件（只发通知） | `IPublisher` | 不应意外调 `Send` |
| 编排层 | `IMediator` | 两者都需要 |

### 3.2 Full Interface Map

```
标记接口（协议声明）:
  IRequest<TResponse>          — REQ
  INotification                — PUB

处理器接口（业务逻辑）:
  IRequestHandler<T, TResponse> — REP
  INotificationHandler<T>      — SUB

调用入口（socket 抽象）:
  ISender                      — REQ 入口
  IPublisher                   — PUB 入口
  IMediator : ISender, IPublisher
```

**7 个类型。不再增加。**

### 3.3 What's NOT Included

| 不包含 | 理由 |
|--------|------|
| `ICommand` / `IQuery` | 用户自己 `: IRequest<T>` 定义。库不区分 |
| `IStreamRequest<T>` | Send + `TResponse` 包含 `IAsyncEnumerable` 已是流式 |
| `IBaseRequest` | 无实际语义的标记接口 |
| `Unit` | 复用 `PicoAop.Abs.VoidResult` |
| `IPipelineBehavior<,>` | PicoAop IInterceptor 完全覆盖 |
| `IRequestPreProcessor<T>` | PicoAop Interceptor 完全覆盖 |
| `IRequestPostProcessor<,>` | PicoAop Interceptor 完全覆盖 |
| `ServiceFactory` | 就是 `ISvcScope.GetService()` |
| Assembly scanning API | 编译期源生成器替代 |


## 4. Relationship with PicoAop

### 4.1 职责边界

```
Mediator = 路由            → "调哪一个 Handler"
AOP      = 装饰            → "怎么调"
```

两者正交。Mediator 不需要知道 AOP 的存在——它只看到 `IRequestHandler` 接口，调用 `Handle()`。如果 DI 返回的是 PicoAop 装饰器代理，对 Mediator 完全透明。

### 4.2 ROUTER = PicoAop

```
ZeroMQ:    REQ ──→ ROUTER ──→ REP
                     ↑
              日志、验证、路由改写

PicoMed:   ISender.Send()
              → [PicoAop] → Handler
                   ↑
             日志、验证、事务 (IInterceptor)
```

ROUTER 不是 REQ/REP 协议的一部分 — 它是独立的中间层。PicoAop 同理。

### 4.3 Pipeline 复用

```csharp
// MediatR 需要:
public class LoggingBehavior<TRequest, TResponse>
    : IPipelineBehavior<TRequest, TResponse> { ... }

// PicoMediator — 直接用 PicoAop:
container.Register<IRequestHandler<CreateOrder, OrderResult>, CreateOrderHandler>()
    .InterceptBy<LoggingInterceptor>()       // ← replace IPipelineBehavior
    .InterceptBy<ValidationInterceptor>()    // ← replace IPipelineBehavior
    .InterceptBy<TransactionInterceptor>();  // ← replace IPipelineBehavior
```

### 4.4 Two-Level Interception

| Level | Scope | PicoAop 用法 |
|-------|-------|-------------|
| Mediator | 全局：所有 Send/Publish | `Register<IMediator, Mediator>().InterceptBy<MetricsInterceptor>()` |
| Handler | 细粒度：特定 Request 类型 | `Register<...Handler>().InterceptBy<LoggingInterceptor>()` |

两层并存，互不冲突。


## 5. Source Generator Strategy

### 5.1 Send Dispatch — Compiled Switch

源生成器扫描所有 `IRequestHandler<T, T>` 实现，生成硬编码 switch 分发：

```csharp
// GeneratedMediatorDispatch.g.cs
internal static class GeneratedMediatorDispatch
{
    public static ValueTask<TResponse> Send<TRequest, TResponse>(
        ISvcScope scope, TRequest request, CancellationToken ct)
        where TRequest : IRequest<TResponse>
    {
        switch (request)
        {
            case CreateOrder co:
            {
                var h = scope.GetService<
                    IRequestHandler<CreateOrder, OrderResult>>();
                return Cast(h.Handle(co, ct));
            }
            case DeleteOrder d:
            {
                var h = scope.GetService<
                    IRequestHandler<DeleteOrder, VoidResult>>();
                return Cast(h.Handle(d, ct));
            }
            // ... one case per concrete/closed-generic request type
        }

        throw new InvalidOperationException(
            $"No handler for {typeof(TRequest)}");
    }

    [MethodImpl(AggressiveInlining)]
    private static ValueTask<TResponse> Cast<TActual, TResponse>(
        ValueTask<TActual> vt)
        => Unsafe.As<ValueTask<TActual>, ValueTask<TResponse>>(ref vt);
}
```

| 指标 | 值 |
|------|-----|
| 分发速度 | ~5ns（JIT 跳转表） |
| 分配 | 零（无委托、无字典、无装箱） |
| 代码量 | 每个 Handler ~8 行，500 Handler ≈ 4000 行 |

### 5.2 Open Generic Handler Support

分四层生成：

| 层 | 扫描目标 | 产出 |
|----|----------|------|
| 1 | `class FooHandler : IRequestHandler<Foo, Bar>` | switch case（具体类型） |
| 2 | `class FooHandler<T> : IRequestHandler<Foo<T>, Bar<T>>` | 元数据记录 |
| 3 | `mediator.Send(new Foo<MyEntity>())` | switch case（封闭泛型使用点） |
| 4 | PicoDI.Gen 协作 | 封闭泛型的 DI 注册 |

PicoMediator.Gen 产出 switch case + 元数据。PicoDI.Gen 产出 DI 注册。各司其职。

### 5.3 Notification — No Generator Needed

`Publish` 用 `scope.GetServices<INotificationHandler<T>>()` — 这是泛型方法，JIT/AOT 为每个具体 `TNotification` 生成代码。`IEnumerable<T>` 注册由 PicoDI.Gen 自动生成。PicoMediator 不需要额外生成。

### 5.4 Module Initializer

源生成器通过 `[ModuleInitializer]` 注册生成的 dispatch 方法。PicoDI 创建容器时自动生效。


## 6. Scope Management

```csharp
public sealed class Mediator : IMediator, IDisposable
{
    private readonly SvcContainer _container;
    private SvcScope? _ambientScope;
    private readonly Lock _scopeLock = new();

    // Mediator 持有 Container（不是 Scope）
    // 每次 Send/Publish 创建或复用子 Scope
    // 匹配 MediatR 的作用域行为
}
```

- Scoped Handler（如 EntityFramework DbContext）在子 Scope 中创建
- 子 Scope 可缓存复用（锁内双检查）
- `Dispose()` 时释放子 Scope


## 7. Error Model

| 场景 | 行为 |
|------|------|
| Send — Handler 抛异常 | 直抛给调用方（不吞没。重试/降级 = AOP） |
| Publish — 多个 Handler 失败 | `AggregateException` |
| Send — 无 Handler | `InvalidOperationException`（编译期源生成器尽早报） |
| Publish — 无订阅者 | 静默返回（ZeroMQ PUB 语义） |


## 8. Project Structure

```
PicoMediator/
├── src/
│   ├── PicoMediator.Abs/          # 7 个接口（netstandard2.0）
│   │   ├── IRequest.cs
│   │   ├── INotification.cs
│   │   ├── IRequestHandler.cs
│   │   ├── INotificationHandler.cs
│   │   ├── ISender.cs
│   │   ├── IPublisher.cs
│   │   └── IMediator.cs
│   │
│   ├── PicoMediator/              # 运行时实现（net10.0）
│   │   ├── Mediator.cs
│   │   └── GeneratedMediatorDispatch.cs  # 静态类，被源生成器填充
│   │
│   ├── PicoMediator.Gen/          # 源生成器（netstandard2.0）
│   │   ├── MediatorGenerator.cs          # 扫描 Handler → switch case
│   │   └── OpenGenericMetadata.cs        # 开放泛型元数据生成
│   │
│   └── PicoMediator.DI/           # DI 集成（net10.0）
│       └── SvcContainerExtensions.cs     # .AddPicoMediator()
│
├── tests/
│   └── PicoMediator.Tests/
│       ├── SendDispatchTests.cs
│       ├── PublishFanoutTests.cs
│       ├── OpenGenericTests.cs
│       ├── GeneratorTests.cs
│       ├── AopIntegrationTests.cs
│       └── ScopeTests.cs
│
├── benchmarks/
│   └── PicoMediator.Benchmarks/
│       ├── SendBenchmarks.cs
│       └── PublishBenchmarks.cs
│
├── samples/
│   └── PicoMediator.Sample/
│       └── Program.cs
│
└── README.md
```

**4 NuGet 包**，遵循 PicoHex 标准 Abs/Core/Gen/DI 四层模式。


## 9. Dependencies

```
PicoMediator.Abs          → 无依赖（独立接口）
PicoMediator              → PicoMediator.Abs + PicoDI + PicoAop.Abs（VoidResult）
PicoMediator.Gen          → PicoMediator.Abs + PicoDI.Gen（复用泛型扫描）+ Microsoft.CodeAnalysis.CSharp
PicoMediator.DI           → PicoMediator + PicoDI
```

PicoAop 是可选的 — 不引 PicoAop 也能用 Mediator 的核心路由，只是没有拦截器链。


## 10. Quick Start

```csharp
// 1. 定义请求
public record CreateOrder(string Item, int Qty) : IRequest<OrderResult>;

public class CreateOrderHandler : IRequestHandler<CreateOrder, OrderResult>
{
    public async ValueTask<OrderResult> Handle(CreateOrder r, CancellationToken ct)
    {
        await SaveToDb(r, ct);
        return new OrderResult(Guid.NewGuid());
    }
}

// 2. 注册（带可选 AOP 管线）
container.AddPicoMediator();
container.Register<IRequestHandler<CreateOrder, OrderResult>, CreateOrderHandler>(Transient)
    .InterceptBy<LoggingInterceptor>()
    .InterceptBy<ValidationInterceptor>();

// 3. 使用
var result = await mediator.Send(new CreateOrder("book", 3));
```


## 11. Comparison: MediatR vs PicoMediator

| | MediatR | PicoMediator |
|------|:---:|:---:|
| Core interfaces | 10+ | 7 |
| Pipeline | `IPipelineBehavior` | Reuse `IInterceptor` |
| Stream requests | `IStreamRequest` | `IRequest<T>` where T wraps `IAsyncEnumerable` |
| Void result | `Unit` | Reuse `VoidResult` |
| Command/Query split | ❌ | ❌ (user-defined via interface inheritance) |
| Registration | Assembly scanning | Source generator + ModuleInitializer |
| Dispatch mechanism | Object factory + reflection | Compiled switch + `Unsafe.As` |
| AOT compatibility | Requires configuration | AOT-first (zero reflection) |
| DI container | Any `IServiceProvider` | PicoDI |
