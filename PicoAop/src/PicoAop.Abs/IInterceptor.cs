namespace PicoAop.Abs;

public interface IInterceptor
{
    TResult Invoke<TResult>(
        IInvocation<TResult> invocation,
        Func<IInvocation<TResult>, TResult> next
    );

    void InvokeVoid(
        IInvocation<PicoDI.Abs.VoidResult> invocation,
        Action<IInvocation<PicoDI.Abs.VoidResult>> next
    );

    ValueTask<TResult> InvokeAsync<TResult>(
        IInvocation<TResult> invocation,
        Func<IInvocation<TResult>, ValueTask<TResult>> next
    );

    ValueTask InvokeAsyncVoid(
        IInvocation<PicoDI.Abs.VoidResult> invocation,
        Func<IInvocation<PicoDI.Abs.VoidResult>, ValueTask> next
    );
}
