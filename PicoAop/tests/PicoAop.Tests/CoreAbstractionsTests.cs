namespace PicoAop.Tests;

public class CoreAbstractionsTests
{
    [Test]
    public async Task VoidResult_IsDefaultStruct()
    {
        var v = new PicoDI.Abs.VoidResult();
        await Assert.That(v.Equals(default(PicoDI.Abs.VoidResult))).IsTrue();
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

    private sealed class PassthroughInterceptor : PicoAop.Abs.InterceptorBase { }

    private sealed class ResultReplacingInterceptor(int replacement) : PicoAop.Abs.InterceptorBase
    {
        public override TResult Invoke<TResult>(
            PicoAop.Abs.IInvocation<TResult> inv,
            Func<PicoAop.Abs.IInvocation<TResult>, TResult> next
        )
        {
            inv.Result = (TResult)(object)replacement;
            return (TResult)(object)replacement;
        }
    }

    private sealed class MockVoidInvocation(string MethodName, Type ServiceType)
        : PicoAop.Abs.IInvocation<PicoDI.Abs.VoidResult>
    {
        public string MethodName { get; } = MethodName;
        public Type ServiceType { get; } = ServiceType;
        public PicoDI.Abs.VoidResult Result { get; set; }
    }

    private sealed class MockInvocation<TResult>(string MethodName, Type ServiceType)
        : PicoAop.Abs.IInvocation<TResult>
    {
        public string MethodName { get; } = MethodName;
        public Type ServiceType { get; } = ServiceType;
        public TResult Result { get; set; } = default!;
    }
}
