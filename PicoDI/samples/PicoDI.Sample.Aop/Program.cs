using System.Diagnostics;
using PicoDI.Generated.Aop;

Console.WriteLine("=== PicoDI AOP / Interceptor Demo ===\n");

// ── How PicoDI AOP works ─────────────────────────────────────────────
// InterceptBy<T>() is a runtime no-op that acts as a compile-time marker.
// The source generator (PicoDI.Gen) detects these calls and emits:
//   1. Invocation structs (per-method, captures parameters)
//   2. Decorator classes (wraps the service, delegates to interceptor)
//   3. A ModuleInitializer that registers decorator chains into the DI
//
// All decorator wiring is resolved at compile time — zero reflection.

var container = new SvcContainer();

// Register interceptors
container.RegisterSingleton<LoggingInterceptor>();
container.RegisterSingleton<TimingInterceptor>();
container.RegisterSingleton<ValidationInterceptor>();
container.RegisterSingleton<RetryInterceptor>(_ => new RetryInterceptor(3));

// Register services with InterceptBy markers (triggers generator)
container.Register<ICalculator>(_ => new Calculator(), SvcLifetime.Scoped)
    .InterceptBy<LoggingInterceptor>();
container.Register<ICalculator>(_ => new Calculator(), SvcLifetime.Scoped)
    .InterceptBy<TimingInterceptor>();
container.Register<ICalculator>(_ => new Calculator(), SvcLifetime.Scoped)
    .InterceptBy<ValidationInterceptor>();
container.Register<IFlakyService>(_ => new FlakyService(), SvcLifetime.Scoped)
    .InterceptBy<RetryInterceptor>();

container.Build();

await using var scope = container.CreateScope();

// Resolve services and interceptors
var calcInner = scope.GetService<ICalculator>()!;
var flaky = scope.GetService<IFlakyService>()!;
var log = scope.GetService<LoggingInterceptor>()!;
var timer = scope.GetService<TimingInterceptor>()!;
var valid = scope.GetService<ValidationInterceptor>()!;
var retry = scope.GetService<RetryInterceptor>()!;

// Build decorator chains manually
// Pattern: new {Service}_{Interceptor}Decorator(inner, interceptor)
var withLog = new ICalculator_LoggingInterceptorDecorator(calcInner, log);
var withTime = new ICalculator_TimingInterceptorDecorator(withLog, timer);
var calc = new ICalculator_ValidationInterceptorDecorator(withTime, valid);

var flakyDecorated = new IFlakyService_RetryInterceptorDecorator(flaky, retry);

// ── Demo ─────────────────────────────────────────────────────────────
Console.WriteLine("--- 1. Logging + Timing + Validation ---");
Console.WriteLine($"  Add(3, 4)     = {calc.Add(3, 4)}");
Console.WriteLine($"  Multiply(6, 7) = {calc.Multiply(6, 7)}");
Console.WriteLine($"  Divide(10, 0)  = {calc.Divide(10, 0)}");
Console.WriteLine();

Console.WriteLine("--- 2. Retry ---");
try { Console.WriteLine($"  TryGetValue()  = {flakyDecorated.TryGetValue(0)}"); }
catch (Exception ex) { Console.WriteLine($"  Failed: {ex.Message}"); }

Console.WriteLine("\n=== AOP Demo Complete ===");

// ════════════════════════════════════════════════════════════════════
public interface ICalculator
{
    int Add(int a, int b);
    int Multiply(int a, int b);
    int Divide(int a, int b);
}

public sealed class Calculator : ICalculator
{
    public int Add(int a, int b) => a + b;
    public int Multiply(int a, int b) => a * b;
    public int Divide(int a, int b) => b != 0 ? a / b : throw new DivideByZeroException();
}

public interface IFlakyService
{
    int TryGetValue(int _);
}

public sealed class FlakyService : IFlakyService
{
    private int _calls;
    public int TryGetValue(int _)
    {
        _calls++;
        if (_calls <= 2) throw new InvalidOperationException($"Svc failed (attempt {_calls})");
        return 42;
    }
}

public sealed class LoggingInterceptor : InterceptorBase
{
    public override TResult Invoke<TResult>(
        IInvocation<TResult> inv, Func<IInvocation<TResult>, TResult> next)
    {
        Console.WriteLine($"  [LOG] {inv.ServiceType.Name}.{inv.MethodName}()");
        var result = next(inv);
        Console.WriteLine($"  [LOG]   → {result}");
        return result;
    }
}

public sealed class TimingInterceptor : InterceptorBase
{
    public override TResult Invoke<TResult>(
        IInvocation<TResult> inv, Func<IInvocation<TResult>, TResult> next)
    {
        var sw = Stopwatch.StartNew();
        var result = next(inv);
        Console.WriteLine($"  [TIMING] {inv.MethodName}: {sw.ElapsedMilliseconds}ms");
        return result;
    }
}

public sealed class ValidationInterceptor : InterceptorBase
{
    public override TResult Invoke<TResult>(
        IInvocation<TResult> inv, Func<IInvocation<TResult>, TResult> next)
    {
        try { return next(inv); }
        catch (DivideByZeroException)
        {
            Console.WriteLine($"  [VALIDATE] caught DivideByZeroException → 0");
            return (TResult)(object)0!;
        }
    }
}

public sealed class RetryInterceptor(int maxRetries) : InterceptorBase
{
    public override TResult Invoke<TResult>(
        IInvocation<TResult> inv, Func<IInvocation<TResult>, TResult> next)
    {
        for (var i = 1; ; i++)
        {
            try { return next(inv); }
            catch (Exception ex) when (i < maxRetries)
            {
                Console.WriteLine($"  [RETRY] attempt {i}: {ex.Message}");
                Thread.Sleep(200 * i);
            }
        }
    }
}
