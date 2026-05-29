
Console.WriteLine("=== PicoAop / Interceptor Demo ===\n");

// ── How PicoAop works ─────────────────────────────────────────────
// InterceptBy<T>() is a runtime no-op that acts as a compile-time marker.
// The source generator (PicoAop.Gen) detects these calls and emits:
//   1. Invocation structs (per-method, captures parameters)
//   2. Decorator classes (wraps the service, delegates to interceptor)
//   3. A ModuleInitializer that auto-registers decorator chains into the DI
//
// All decorator wiring is resolved at compile time — zero reflection.

var container = new SvcContainer();

// Register interceptors
container.RegisterSingleton<LoggingInterceptor>();
container.RegisterSingleton<TimingInterceptor>();

// Register services with InterceptBy markers — these are compile-time markers
// that trigger the source generator to emit decorator class definitions
container.Register<IGreeter, Greeter>(SvcLifetime.Scoped).InterceptBy<LoggingInterceptor>();
container.Register<IGreeter, Greeter>(SvcLifetime.Scoped).InterceptBy<TimingInterceptor>();

container.Build();

await using var scope = container.CreateScope();

var greeter = scope.GetService<IGreeter>()!;
var log = scope.GetService<LoggingInterceptor>()!;
var timer = scope.GetService<TimingInterceptor>()!;

// Build decorator chain manually (generated types are in PicoAop.Generated namespace)
// Generated: PicoAop.Generated.IGreeter_LoggingInterceptorDecorator
// Generated: PicoAop.Generated.IGreeter_TimingInterceptorDecorator

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

public sealed class LoggingInterceptor : InterceptorBase
{
    public override TResult Invoke<TResult>(
        IInvocation<TResult> inv,
        Func<IInvocation<TResult>, TResult> next
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
    public override TResult Invoke<TResult>(
        IInvocation<TResult> inv,
        Func<IInvocation<TResult>, TResult> next
    )
    {
        var sw = Stopwatch.StartNew();
        var result = next(inv);
        Console.WriteLine($"  [TIMING] {inv.MethodName}: {sw.ElapsedMilliseconds}ms");
        return result;
    }
}
