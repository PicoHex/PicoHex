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

var container = new SvcContainer();

// Register interceptors as services (resolved by DI when building the chain)
container.RegisterSingleton<LoggingInterceptor>();
container.RegisterSingleton<TimingInterceptor>();

// InterceptBy<T>() markers trigger source generation:
// PicoAop.Gen generates Invocation structs + proxy + wrappers
// PicoDI.Gen rewrites registration to use PicoAopWrappers.Wrap_*()
container.RegisterScoped<IGreeter, Greeter>().InterceptBy<LoggingInterceptor>();
container.RegisterScoped<IGreeter, Greeter>().InterceptBy<TimingInterceptor>();

container.Build();
await using var scope = container.CreateScope();

var greeter = scope.GetService<IGreeter>()!;
Console.WriteLine(greeter.Greet("World"));
Console.WriteLine("\n=== AOP Demo Complete ===");

// ════════════════════════════════════════════════════════════════════
public interface IGreeter
{
    string Greet(string name);
}

public sealed class Greeter : IGreeter
{
    public string Greet(string name) => $"Hello, {name}!";
}

// Zero-allocation interceptor — uses struct generic Invocation,
// no boxing, no reflection, fully AOT compatible
public sealed class LoggingInterceptor : InterceptorBase
{
    public override TResult Invoke<TInvocation, TResult>(
        TInvocation inv,
        Func<TInvocation, TResult> next
    )
    {
        Console.WriteLine($"  [LOG] {inv.ServiceType.Name}.{inv.MethodName}()");
        var result = next(inv);
        Console.WriteLine($"  [LOG]   -> {result}");
        return result;
    }
}

public sealed class TimingInterceptor : InterceptorBase
{
    public override TResult Invoke<TInvocation, TResult>(
        TInvocation inv,
        Func<TInvocation, TResult> next
    )
    {
        var sw = Stopwatch.StartNew();
        var result = next(inv);
        Console.WriteLine($"  [TIMING] {inv.MethodName}: {sw.ElapsedMilliseconds}ms");
        return result;
    }
}
