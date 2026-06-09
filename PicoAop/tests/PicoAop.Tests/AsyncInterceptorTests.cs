namespace PicoAop.Tests.Async;

public interface IAsyncSvc
{
    Task<int> GetCountAsync();
    ValueTask ResetAsync();
}

sealed class AsyncSvc : IAsyncSvc
{
    public int Count;
    public Task<int> GetCountAsync() => Task.FromResult(Count);
    public ValueTask ResetAsync() { Count = 0; return default; }
}

// Simulated SG-generated types — TResult is the UNWRAPPED type

struct Invocation_IAsyncSvc_GetCountAsync : IInvocation<int>
{
    internal IAsyncSvc _target;
    internal IInterceptor _i0;

    public Invocation_IAsyncSvc_GetCountAsync(IAsyncSvc target, IInterceptor i0)
    { _target = target; _i0 = i0; Result = default; }

    public string MethodName => "GetCountAsync";
    public Type ServiceType => typeof(IAsyncSvc);
    public int Result { get; set; }

    internal async ValueTask<int> InvokeTargetAsync() => await _target.GetCountAsync();
}

struct Invocation_IAsyncSvc_ResetAsync : IInvocation
{
    internal IAsyncSvc _target;
    internal IInterceptor _i0;

    public Invocation_IAsyncSvc_ResetAsync(IAsyncSvc target, IInterceptor i0)
    { _target = target; _i0 = i0; }

    public string MethodName => "ResetAsync";
    public Type ServiceType => typeof(IAsyncSvc);

    internal async ValueTask InvokeTargetAsync() => await _target.ResetAsync();
}

sealed class Intercepted_IAsyncSvc : IAsyncSvc
{
    private readonly IAsyncSvc _inner;
    private readonly IInterceptor _i0;

    private static readonly Func<Invocation_IAsyncSvc_GetCountAsync, ValueTask<int>> s_getNext = static inv => inv.InvokeTargetAsync();
    private static readonly Func<Invocation_IAsyncSvc_ResetAsync, ValueTask> s_resetNext = static inv => inv.InvokeTargetAsync();

    public Intercepted_IAsyncSvc(IAsyncSvc inner, IInterceptor i0)
    { _inner = inner; _i0 = i0; }

    public Task<int> GetCountAsync()
    {
        var inv = new Invocation_IAsyncSvc_GetCountAsync(_inner, _i0);
        return _i0.InvokeAsync(inv, s_getNext).AsTask();
    }

    public ValueTask ResetAsync()
    {
        var inv = new Invocation_IAsyncSvc_ResetAsync(_inner, _i0);
        return _i0.InvokeAsyncVoid(inv, s_resetNext);
    }
}

sealed class LoggingAsyncInterceptor : InterceptorBase
{
    public int CallCount;

    public override ValueTask<TResult> InvokeAsync<TInvocation, TResult>(TInvocation inv, Func<TInvocation, ValueTask<TResult>> next)
    { CallCount++; return next(inv); }

    public override ValueTask InvokeAsyncVoid<TInvocation>(TInvocation inv, Func<TInvocation, ValueTask> next)
    { CallCount++; return next(inv); }
}

public class AsyncInterceptorTests
{
    [Test]
    public async Task AsyncTaskMethod_CallsInterceptor()
    {
        var interceptor = new LoggingAsyncInterceptor();
        var proxy = new Intercepted_IAsyncSvc(new AsyncSvc { Count = 42 }, interceptor);

        var result = await proxy.GetCountAsync();

        await Assert.That(result).IsEqualTo(42);
        await Assert.That(interceptor.CallCount).IsEqualTo(1);
    }

    [Test]
    public async Task AsyncValueTaskMethod_GoesThroughInterceptor()
    {
        var svc = new AsyncSvc { Count = 10 };
        var interceptor = new LoggingAsyncInterceptor();
        var proxy = new Intercepted_IAsyncSvc(svc, interceptor);

        await proxy.ResetAsync();

        await Assert.That(svc.Count).IsEqualTo(0);
        await Assert.That(interceptor.CallCount).IsEqualTo(1);
    }
}
