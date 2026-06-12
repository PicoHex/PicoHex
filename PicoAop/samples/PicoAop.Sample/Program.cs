Console.WriteLine("=== PicoAop / Interceptor Demo ===\n");

// ── How PicoAop works ─────────────────────────────────────────────
// InterceptBy<T>() is a runtime no-op that acts as a compile-time marker.
// PicoAop.Gen detects these calls and emits:
//   1. Invocation structs (per-method, zero-allocation, struct generics)
//   2. Proxy classes (implements the service interface)
//   3. PicoAopWrappers.Wrap_*() static factory methods
//
// PicoDI.Gen detects the InterceptBy chains and rewrites registrations
// to use the generated wrappers — all at compile time, zero reflection.

// ════════════════════════════════════════════════════════════════════
// 1. Sync Interceptor Chain
// ════════════════════════════════════════════════════════════════════

Console.WriteLine("── 1. Sync Interceptor Chain ──\n");

var container = new SvcContainer();

container.RegisterSingleton<LoggingInterceptor>();
container.RegisterSingleton<TimingInterceptor>();

container
    .RegisterScoped<IGreeter, Greeter>()
    .InterceptBy<LoggingInterceptor>()
    .InterceptBy<TimingInterceptor>();

container.Build();
await using var scope = container.CreateScope();

var greeter = scope.GetService<IGreeter>()!;
Console.WriteLine(greeter.Greet("World"));

// ════════════════════════════════════════════════════════════════════
// 2. Async Interceptor (ValueTask return)
// ════════════════════════════════════════════════════════════════════

Console.WriteLine("\n── 2. Async Interceptor ──\n");

var container2 = new SvcContainer();
container2.RegisterSingleton<AsyncTimingInterceptor>();
container2.RegisterScoped<IAsyncWorker, AsyncWorker>().InterceptBy<AsyncTimingInterceptor>();
container2.Build();
await using var scope2 = container2.CreateScope();

var worker = scope2.GetService<IAsyncWorker>()!;
var result = await worker.FetchDataAsync("user-42");
Console.WriteLine($"   Result: {result}");

// ── Fire-and-forget (async void) ─────────────────────────────────
await worker.NotifyAsync("system-ready");
Console.WriteLine("   Notify dispatched (fire-and-forget).\n");

// ════════════════════════════════════════════════════════════════════
// 3. Global Interceptor (applied to ALL services)
// ════════════════════════════════════════════════════════════════════

Console.WriteLine("── 3. Global Interceptor ──\n");

var container3 = new SvcContainer();
container3.RegisterSingleton<GlobalAuditInterceptor>();

// AddInterceptor<T>() applies the interceptor to every registered service
container3.AddInterceptor<GlobalAuditInterceptor>();

container3.RegisterScoped<IGreeter, Greeter>();
container3.RegisterScoped<IAsyncWorker, AsyncWorker>();
container3.Build();
await using var scope3 = container3.CreateScope();

var g3 = scope3.GetService<IGreeter>()!;
Console.WriteLine(g3.Greet("Global"));

var w3 = scope3.GetService<IAsyncWorker>()!;
await w3.FetchDataAsync("global-1");

// ════════════════════════════════════════════════════════════════════
// 4. WithoutInterceptors / WithoutInterceptor
// ════════════════════════════════════════════════════════════════════

Console.WriteLine("\n── 4. Per-Registration Interceptor Control ──\n");

var container4 = new SvcContainer();
container4.AddInterceptor<GlobalAuditInterceptor>(); // global default
container4.RegisterSingleton<LoggingInterceptor>();

// .InterceptBy<T>() adds a specific interceptor
container4.RegisterScoped<IGreeter, Greeter>().InterceptBy<LoggingInterceptor>();

// .WithoutInterceptors() removes ALL interceptors from this registration
container4.RegisterScoped<IAsyncWorker, AsyncWorker>().WithoutInterceptors();

// .WithoutInterceptor<T>() removes a specific interceptor
// (e.g., opt out of the global audit but keep per-service logging)
container4
    .RegisterScoped<IBackgroundTask, BackgroundTask>()
    .WithoutInterceptor<GlobalAuditInterceptor>()
    .InterceptBy<LoggingInterceptor>();

container4.Build();
await using var scope4 = container4.CreateScope();

var g4 = scope4.GetService<IGreeter>()!;
Console.WriteLine("   IGreeter (Global + Logging):");
Console.WriteLine(g4.Greet("Controlled"));

var w4 = scope4.GetService<IAsyncWorker>()!;
Console.WriteLine("   IAsyncWorker (No Interceptors):");
await w4.FetchDataAsync("bare");

var bt4 = scope4.GetService<IBackgroundTask>()!;
Console.WriteLine("   IBackgroundTask (Logging only, no Global):");
bt4.Execute("cleanup");

Console.WriteLine("\n=== AOP Demo Complete ===");

// ════════════════════════════════════════════════════════════════════
// Service Interfaces & Implementations
// ════════════════════════════════════════════════════════════════════

public interface IGreeter
{
    string Greet(string name);
}

public sealed class Greeter : IGreeter
{
    public string Greet(string name) => $"Hello, {name}!";
}

public interface IAsyncWorker
{
    ValueTask<string> FetchDataAsync(string key);
    ValueTask NotifyAsync(string message);
}

public sealed class AsyncWorker : IAsyncWorker
{
    public async ValueTask<string> FetchDataAsync(string key)
    {
        await Task.Delay(10); // simulated I/O
        return $"data-for-{key}";
    }

    public async ValueTask NotifyAsync(string message)
    {
        await Task.Delay(5);
        Console.WriteLine($"     [Notify] {message}");
    }
}

public interface IBackgroundTask
{
    void Execute(string name);
}

public sealed class BackgroundTask : IBackgroundTask
{
    public void Execute(string name) => Console.WriteLine($"     [Task] Executing {name}.");
}

// ════════════════════════════════════════════════════════════════════
// Interceptors
// ════════════════════════════════════════════════════════════════════

// ── Sync interceptor (covers both void and return paths) ──
public sealed class LoggingInterceptor : InterceptorBase
{
    public override TResult Invoke<TInvocation, TResult>(
        TInvocation inv,
        Func<TInvocation, TResult> next
    )
    {
        Console.WriteLine($"     [LOG] {inv.ServiceType.Name}.{inv.MethodName}()");
        var result = next(inv);
        Console.WriteLine($"     [LOG]   -> {result}");
        return result;
    }

    public override void InvokeVoid<TInvocation>(TInvocation inv, Func<TInvocation, object?> next)
    {
        Console.WriteLine($"     [LOG] {inv.ServiceType.Name}.{inv.MethodName}() (void)");
        next(inv);
    }
}

// ── Sync-return interceptor with timing ────────────────────────────
public sealed class TimingInterceptor : InterceptorBase
{
    public override TResult Invoke<TInvocation, TResult>(
        TInvocation inv,
        Func<TInvocation, TResult> next
    )
    {
        var sw = Stopwatch.StartNew();
        var result = next(inv);
        Console.WriteLine($"     [TIMING] {inv.MethodName}: {sw.ElapsedMilliseconds}ms");
        return result;
    }
}

// ── Async-return interceptor (override InvokeAsync<TInvocation, TResult>) ──
public sealed class AsyncTimingInterceptor : InterceptorBase
{
    public override async ValueTask<TResult> InvokeAsync<TInvocation, TResult>(
        TInvocation inv,
        Func<TInvocation, ValueTask<TResult>> next
    )
    {
        Console.WriteLine($"     [ASYNC] {inv.MethodName}() started");
        var sw = Stopwatch.StartNew();
        var result = await next(inv);
        Console.WriteLine(
            $"     [ASYNC] {inv.MethodName}() completed in {sw.ElapsedMilliseconds}ms"
        );
        return result;
    }

    // Also intercept async void methods
    public override async ValueTask InvokeAsyncVoid<TInvocation>(
        TInvocation inv,
        Func<TInvocation, ValueTask> next
    )
    {
        Console.WriteLine($"     [ASYNC-VOID] {inv.MethodName}() started");
        await next(inv);
        Console.WriteLine($"     [ASYNC-VOID] {inv.MethodName}() completed");
    }
}

// ── Global interceptor (applied to every service via AddInterceptor<T>) ──
public sealed class GlobalAuditInterceptor : InterceptorBase
{
    public override TResult Invoke<TInvocation, TResult>(
        TInvocation inv,
        Func<TInvocation, TResult> next
    )
    {
        Console.WriteLine($"     [AUDIT] → {inv.ServiceType.Name}.{inv.MethodName}");
        return next(inv);
    }

    public override void InvokeVoid<TInvocation>(TInvocation inv, Func<TInvocation, object?> next)
    {
        Console.WriteLine($"     [AUDIT] → {inv.ServiceType.Name}.{inv.MethodName} (void)");
        next(inv);
    }

    public override async ValueTask<TResult> InvokeAsync<TInvocation, TResult>(
        TInvocation inv,
        Func<TInvocation, ValueTask<TResult>> next
    )
    {
        Console.WriteLine($"     [AUDIT] → {inv.ServiceType.Name}.{inv.MethodName} (async)");
        return await next(inv);
    }

    public override async ValueTask InvokeAsyncVoid<TInvocation>(
        TInvocation inv,
        Func<TInvocation, ValueTask> next
    )
    {
        Console.WriteLine($"     [AUDIT] → {inv.ServiceType.Name}.{inv.MethodName} (async-void)");
        await next(inv);
    }
}
