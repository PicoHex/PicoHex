namespace PicoAop.Tests.Chain;

#region Simulated SG-generated types

public interface ICalc
{
    int Multiply(int x, int y);
    string? Name { get; set; }
}

sealed class Calc : ICalc
{
    public int Multiply(int x, int y) => x * y;
    public string? Name { get; set; } = "calc";
}

struct Invocation_ICalc_Multiply : IInvocation<int>
{
    internal ICalc _target;
    internal IInterceptor _i0;
    internal int _x;
    internal int _y;
    public int Result { get; set; }

    public Invocation_ICalc_Multiply(ICalc target, IInterceptor i0, int x, int y)
    { _target = target; _i0 = i0; _x = x; _y = y; Result = default; }

    public string MethodName => "Multiply";
    public Type ServiceType => typeof(ICalc);
    internal int InvokeTarget() => _target.Multiply(_x, _y);
}

struct Invocation_ICalc_Name_Getter : IInvocation<string?>
{
    internal ICalc _target;
    internal IInterceptor _i0;
    public string? Result { get; set; }

    public Invocation_ICalc_Name_Getter(ICalc target, IInterceptor i0)
    { _target = target; _i0 = i0; Result = default; }

    public string MethodName => "get_Name";
    public Type ServiceType => typeof(ICalc);
    internal string? InvokeTarget() => _target.Name;
}

sealed class Intercepted_ICalc : ICalc
{
    private readonly ICalc _inner;
    private readonly IInterceptor _i0;

    private static readonly Func<Invocation_ICalc_Multiply, int> s_multiplyNext = static inv => inv.InvokeTarget();
    private static readonly Func<Invocation_ICalc_Name_Getter, string?> s_get_NameNext = static inv => inv.InvokeTarget();

    public Intercepted_ICalc(ICalc inner, IInterceptor i0)
    { _inner = inner; _i0 = i0; }

    public int Multiply(int x, int y)
    {
        var inv = new Invocation_ICalc_Multiply(_inner, _i0, x, y);
        return _i0.Invoke(inv, s_multiplyNext);
    }

    public string? Name
    {
        get
        {
            var inv = new Invocation_ICalc_Name_Getter(_inner, _i0);
            return _i0.Invoke(inv, s_get_NameNext);
        }
        set => _inner.Name = value;
    }
}

sealed class CallbackInterceptor : InterceptorBase
{
    public string? LastMethod;
    public int CallCount;

    public override TResult Invoke<TInvocation, TResult>(TInvocation inv, Func<TInvocation, TResult> next)
    {
        CallCount++;
        LastMethod = inv.MethodName;
        return next(inv);
    }
}

#endregion

public class RuntimeChainTests
{
    [Test]
    public async Task InterceptedMethod_ReturnsCorrectValue()
    {
        var interceptor = new CallbackInterceptor();
        var proxy = new Intercepted_ICalc(new Calc(), interceptor);

        var result = proxy.Multiply(3, 7);

        await Assert.That(result).IsEqualTo(21);
    }

    [Test]
    public async Task InterceptedMethod_InvokesInterceptor()
    {
        var interceptor = new CallbackInterceptor();
        var proxy = new Intercepted_ICalc(new Calc(), interceptor);

        proxy.Multiply(5, 6);

        await Assert.That(interceptor.CallCount).IsEqualTo(1);
        await Assert.That(interceptor.LastMethod).IsEqualTo("Multiply");
    }

    [Test]
    public async Task PropertyGetter_GoesThroughInterceptor()
    {
        var interceptor = new CallbackInterceptor();
        var proxy = new Intercepted_ICalc(new Calc(), interceptor);

        var name = proxy.Name;

        await Assert.That(name).IsEqualTo("calc");
        await Assert.That(interceptor.CallCount).IsEqualTo(1);
        await Assert.That(interceptor.LastMethod).IsEqualTo("get_Name");
    }

    [Test]
    public async Task MultipleCalls_AllIntercepted()
    {
        var interceptor = new CallbackInterceptor();
        var proxy = new Intercepted_ICalc(new Calc(), interceptor);

        proxy.Multiply(1, 2);
        proxy.Multiply(3, 4);
        proxy.Multiply(5, 6);

        await Assert.That(interceptor.CallCount).IsEqualTo(3);
    }
}
