using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace PicoAop.Tests;

public class RuntimeAsyncInterceptorTests
{
    public interface IAsyncCalc
    {
        Task<int> AddAsync(int a, int b);
    }

    public sealed class AsyncCalc : IAsyncCalc
    {
        public Task<int> AddAsync(int a, int b) => Task.FromResult(a + b);
    }

    public interface IWorker
    {
        ValueTask WorkAsync();
    }

    public sealed class Worker : IWorker
    {
        public ValueTask WorkAsync() => ValueTask.CompletedTask;
    }

    public sealed class AsyncRecordingInterceptor : InterceptorBase
    {
        public List<string> Calls { get; } = [];

        public override ValueTask<TResult> InvokeAsync<TResult>(
            IInvocation<TResult> inv,
            Func<IInvocation<TResult>, ValueTask<TResult>> next
        )
        {
            Calls.Add($"InvokeAsync:{inv.MethodName}");
            return next(inv);
        }

        public override ValueTask InvokeAsyncVoid(
            IInvocation<PicoDI.Abs.VoidResult> inv,
            Func<IInvocation<PicoDI.Abs.VoidResult>, ValueTask> next
        )
        {
            Calls.Add($"InvokeAsyncVoid:{inv.MethodName}");
            return next(inv);
        }
    }

    public sealed class ResultModifyingInterceptor(int replacement) : InterceptorBase
    {
        public override async ValueTask<TResult> InvokeAsync<TResult>(
            IInvocation<TResult> inv,
            Func<IInvocation<TResult>, ValueTask<TResult>> next
        )
        {
            var result = await next(inv);
            inv.Result = (TResult)(object)replacement;
            return (TResult)(object)replacement;
        }
    }

    public sealed class ThrowingAsyncInterceptor : InterceptorBase
    {
        public override ValueTask<TResult> InvokeAsync<TResult>(
            IInvocation<TResult> inv,
            Func<IInvocation<TResult>, ValueTask<TResult>> next
        ) => throw new InvalidOperationException("async failure");
    }

    // Trigger the source generator to emit decorator types.
    public static void TriggerGen(SvcContainer c)
    {
        c.Register<IAsyncCalc, AsyncCalc>(SvcLifetime.Scoped)
            .InterceptBy<AsyncRecordingInterceptor>();
        c.Register<IWorker, Worker>(SvcLifetime.Scoped).InterceptBy<AsyncRecordingInterceptor>();
        c.Register<IAsyncCalc, AsyncCalc>(SvcLifetime.Scoped)
            .InterceptBy<ResultModifyingInterceptor>();
        c.Register<IAsyncCalc, AsyncCalc>(SvcLifetime.Scoped)
            .InterceptBy<ThrowingAsyncInterceptor>();
    }

    private static TDecorator CreateDecorator<TService, TDecorator>(
        TService inner,
        IInterceptor interceptor
    )
        where TDecorator : TService
    {
        var ctor = typeof(TDecorator).GetConstructors().First();
        return (TDecorator)ctor.Invoke([inner, interceptor]);
    }

    [Test]
    public async Task TaskOfT_Method_Invokes_InvokeAsync()
    {
        var inner = new AsyncCalc();
        var interceptor = new AsyncRecordingInterceptor();
        var decorator = CreateDecorator<
            IAsyncCalc,
            PicoAop_Tests_RuntimeAsyncInterceptorTests_IAsyncCalc_AsyncRecordingInterceptorDecorator
        >(inner, interceptor);

        var result = await decorator.AddAsync(3, 4);
        await Assert.That(result).IsEqualTo(7);
        await Assert.That(interceptor.Calls).Contains("InvokeAsync:AddAsync");
    }

    [Test]
    public async Task ValueTask_Method_Invokes_InvokeAsyncVoid()
    {
        var inner = new Worker();
        var interceptor = new AsyncRecordingInterceptor();
        var decorator = CreateDecorator<
            IWorker,
            PicoAop_Tests_RuntimeAsyncInterceptorTests_IWorker_AsyncRecordingInterceptorDecorator
        >(inner, interceptor);

        await decorator.WorkAsync();
        await Assert.That(interceptor.Calls).Contains("InvokeAsyncVoid:WorkAsync");
    }

    [Test]
    public async Task AsyncInterceptor_CanModifyResult()
    {
        var inner = new AsyncCalc();
        var interceptor = new ResultModifyingInterceptor(99);
        var decorator = CreateDecorator<
            IAsyncCalc,
            PicoAop_Tests_RuntimeAsyncInterceptorTests_IAsyncCalc_ResultModifyingInterceptorDecorator
        >(inner, interceptor);

        var result = await decorator.AddAsync(1, 2);
        await Assert.That(result).IsEqualTo(99);
    }

    [Test]
    public async Task AsyncInterceptor_ExceptionPropagates()
    {
        var inner = new AsyncCalc();
        var interceptor = new ThrowingAsyncInterceptor();
        var decorator = CreateDecorator<
            IAsyncCalc,
            PicoAop_Tests_RuntimeAsyncInterceptorTests_IAsyncCalc_ThrowingAsyncInterceptorDecorator
        >(inner, interceptor);

        await Assert.ThrowsAsync(async () => await decorator.AddAsync(1, 2));
    }
}
