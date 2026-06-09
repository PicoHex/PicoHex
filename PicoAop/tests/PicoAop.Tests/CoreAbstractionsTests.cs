namespace PicoAop.Tests;

/// <summary>
/// Simulates what PicoAop.Gen will generate: per-method Invocation struct + proxy class.
/// </summary>

#region Simulated SG-generated types

public interface ICalc
{
    int Add(int a, int b);
}

sealed class Calc : ICalc
{
    public int Add(int a, int b) => a + b;
}

struct Invocation_ICalc_Add : IInvocation<int>
{
    internal readonly ICalc _target;
    internal readonly IInterceptor _i0;
    internal readonly int _a;
    internal readonly int _b;

    public Invocation_ICalc_Add(ICalc target, IInterceptor i0, int a, int b)
    { _target = target; _i0 = i0; _a = a; _b = b; Result = default; }

    public string MethodName => "Add";
    public Type ServiceType => typeof(ICalc);
    public int Result { get; set; }

    internal int InvokeTarget() => _target.Add(_a, _b);
}

sealed class Intercepted_ICalc : ICalc
{
    private readonly ICalc _inner;
    private readonly IInterceptor _i0;
    private static readonly Func<Invocation_ICalc_Add, int> s_addNext = static inv => inv.InvokeTarget();

    public Intercepted_ICalc(ICalc inner, IInterceptor i0)
    { _inner = inner; _i0 = i0; }

    public int Add(int a, int b)
    {
        var inv = new Invocation_ICalc_Add(_inner, _i0, a, b);
        return _i0.Invoke(inv, s_addNext);
    }
}

#endregion

public class CoreAbstractionsTests
{
    [Test]
    public async Task ZeroBoxingInvoke_ReturnsCorrectResult()
    {
        var interceptor = new PassThroughInterceptor();
        var proxy = new Intercepted_ICalc(new Calc(), interceptor);

        var result = proxy.Add(3, 7);

        await Assert.That(result).IsEqualTo(10);
    }

    [Test]
    public async Task Interceptor_IsCalledOnEachInvocation()
    {
        var interceptor = new CountingInterceptor();
        var proxy = new Intercepted_ICalc(new Calc(), interceptor);

        proxy.Add(1, 2);
        proxy.Add(3, 4);
        proxy.Add(5, 6);

        await Assert.That(interceptor.CallCount).IsEqualTo(3);
    }
}

sealed class PassThroughInterceptor : InterceptorBase { }

sealed class CountingInterceptor : InterceptorBase
{
    public int CallCount;

    public override TResult Invoke<TInvocation, TResult>(TInvocation inv, Func<TInvocation, TResult> next)
    {
        CallCount++;
        return next(inv);
    }
}
