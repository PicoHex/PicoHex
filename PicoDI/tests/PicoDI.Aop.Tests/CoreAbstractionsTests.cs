namespace PicoDI.Aop.Tests;

public class CoreAbstractionsTests
{
    [Test]
    public async Task VoidResult_IsDefaultStruct()
    {
        var v = new VoidResult();
        await Assert.That(v.Equals(default(VoidResult))).IsTrue();
    }

    [Test]
    public async Task IInvocation_MethodName_ReturnsMethodName()
    {
        var inv = new MockInvocation<int>(MethodName: "Test", ServiceType: typeof(string));
        await Assert.That(inv.MethodName).IsEqualTo("Test");
    }

    [Test]
    public async Task IInvocation_ResultIsWritable()
    {
        var interceptor = new ResultReplacingInterceptor(99);
        var inv = new MockInvocation<int>(MethodName: "Test", ServiceType: typeof(string));

        var result = interceptor.Invoke(inv, i => 42);

        await Assert.That(result).IsEqualTo(99);
        await Assert.That(inv.Result).IsEqualTo(99);
    }

    [Test]
    public async Task IInvocation_ScopeCanBeNull()
    {
        var inv = new MockInvocation<int>(MethodName: "Test", ServiceType: typeof(string));
        await Assert.That(inv.Scope).IsNull();
    }

    [Test]
    public async Task InterceptorBase_DefaultInvoke_PassesThroughResult()
    {
        var interceptor = new PassthroughInterceptor();
        var inv = new MockInvocation<int>(MethodName: "Test", ServiceType: typeof(string));

        var result = interceptor.Invoke(inv, i => 42);

        await Assert.That(result).IsEqualTo(42);
    }

    [Test]
    public async Task InterceptorBase_DefaultInvokeVoid_CallsNext()
    {
        var called = false;
        var interceptor = new PassthroughInterceptor();
        var inv = new MockVoidInvocation(MethodName: "VoidTest", ServiceType: typeof(string));

        interceptor.InvokeVoid(
            inv,
            _ =>
            {
                called = true;
            }
        );

        await Assert.That(called).IsTrue();
    }

    private sealed class PassthroughInterceptor : InterceptorBase { }

    private sealed class ResultReplacingInterceptor(int replacement) : InterceptorBase
    {
        public override TResult Invoke<TResult>(
            IInvocation<TResult> inv,
            Func<IInvocation<TResult>, TResult> next
        )
        {
            inv.Result = (TResult)(object)replacement;
            return (TResult)(object)replacement;
        }
    }

    private sealed class MockInvocation<TResult>(
        string MethodName,
        Type ServiceType,
        ISvcScope? Scope = null
    ) : IInvocation<TResult>
    {
        public string MethodName { get; } = MethodName;
        public Type ServiceType { get; } = ServiceType;
        public ISvcScope? Scope { get; } = Scope;
        public TResult Result { get; set; } = default!;
    }

    private sealed class MockVoidInvocation(
        string MethodName,
        Type ServiceType,
        ISvcScope? Scope = null
    ) : IInvocation<VoidResult>
    {
        public string MethodName { get; } = MethodName;
        public Type ServiceType { get; } = ServiceType;
        public ISvcScope? Scope { get; } = Scope;
        public VoidResult Result { get; set; }
    }
}
