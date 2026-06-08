# PicoAot

**AOT-First Compile-Time Interception Engine**

A next-generation replacement for PicoAop — zero-allocation, zero-boxing, zero-reflection method interception for AOT-compiled .NET applications.

[![NuGet](https://img.shields.io/nuget/v/PicoAot.Abs)](https://nuget.org/packages/PicoAot.Abs)
[![License: MIT](https://img.shields.io/badge/License-MIT-green.svg)](https://github.com/PicoHex/PicoHex/blob/main/LICENSE)

---

## Why PicoAot?

| | PicoAop (legacy) | PicoAot |
|---|---|---|
| **Invocation** | Interface boxing (`IInvocation<T>`) | Struct generics with static delegate cache |
| **Allocation per call** | 1+ heap objects (boxing + async state machine) | **Zero** — stack-only struct, cached `Func<>` |
| **Async overhead** | Per-method async lambda | Static cached `Func<TInvocation, ValueTask<T>>` |
| **Interceptor API** | `delegate*` unsafe (requires `AllowUnsafeBlocks`) | Safe managed `Func<TInvocation, TResult>` |
| **Property interception** | Direct delegation (bypasses interceptor) | **Supported** — getter/setter each have own Invocation struct |

---

## Quick Start

```shell
dotnet add package PicoAot.Abs
```

```csharp
using PicoAot.Abs;

public sealed class LoggingInterceptor : InterceptorBase
{
    public int CallCount;

    public override TResult Invoke<TInvocation, TResult>(
        TInvocation inv, Func<TInvocation, TResult> next)
    {
        CallCount++;
        Console.WriteLine($"Calling {inv.MethodName}");
        return next(inv);
    }
}
```

### Register + Intercept

PicoDI.Gen detects `.InterceptBy<T>()` and rewrites the registration automatically:

```csharp
container.RegisterScoped<IService, MyService>()
    .InterceptBy<LoggingInterceptor>();
```

PicoAot.Gen generates:
1. **Invocation struct** per method — implements `IInvocation<TResult>`, holds target + interceptor + parameters
2. **Proxy class** — implements the service interface, calls interceptor pipeline
3. **Static delegate cache** — one `Func<>` per method, cached once, zero allocation per call

All without unsafe code, without boxing, without runtime reflection.

---

## How It Works

```
User code: Register<ISvc, Impl>().InterceptBy<MyInterceptor>()
                    │                                  │
          PicoDI.Gen detects                   PicoAot.Gen generates
          & rewrites registration              Invocation structs +
                                               proxy class + wrappers
                    │                                  │
                    └──────────────┬───────────────────┘
                                   ▼
                    container.Register<ISvc>(scope =>
                        PicoAotWrappers.Wrap_ISvc(
                            scope.GetService<Impl>(),
                            scope.GetService<MyInterceptor>()))
```

### Invocation Struct (SG-generated)

```csharp
struct Invocation_ISvc_Execute : IInvocation<int>
{
    internal ISvc _target;
    internal MyInterceptor _i0;
    internal int _param1;

    public string MethodName => "Execute";
    public Type ServiceType => typeof(ISvc);
    public int Result { get; set; }

    internal int InvokeTarget() => _target.Execute(_param1);
}
```

### Proxy Class (SG-generated)

```csharp
sealed class Intercepted_ISvc : ISvc
{
    private readonly ISvc _inner;
    private readonly MyInterceptor _i0;

    private static readonly Func<Invocation_ISvc_Execute, int> s_executeNext
        = static inv => inv.InvokeTarget();

    public int Execute(int param1)
    {
        var inv = new Invocation_ISvc_Execute(_inner, _i0, param1);
        return _i0.Invoke(inv, s_executeNext);
    }
}
```

---

## Packages

| Package | Description |
|---|---|
| **PicoAot.Abs** | `IInterceptor`, `IInvocation`, `InterceptorBase` — zero-boxing interceptor abstractions |
| **PicoAot.Gen** | Source generator — emits Invocation structs, proxy classes, and `PicoAotWrappers` factories |

---

## Constraints

| Target | Support | Guidance |
|--------|---------|----------|
| Interface | ✅ Full support — struct invocation + class proxy | Preferred approach |
| Virtual method class | ✅ Override-based proxy | Works but prefer interfaces |
| Sealed class | ❌ `PICO101` compile error | Extract an interface |
| Struct | ❌ `PICO102` compile error | Extract an interface |
| `ref`/`out` params | ⚠️ `PICO110` warning — direct delegation | Methods with ref params skip interception |
| Async `Task<T>` / `ValueTask<T>` | ✅ `InvokeTargetAsync()` returns `ValueTask<T>`, proxy handles `.AsTask()` | Fully supported |
| Property getter/setter | ✅ Each has own Invocation struct | Fully intercepted |
