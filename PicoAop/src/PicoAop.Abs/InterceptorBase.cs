namespace PicoAop.Abs;

public abstract class InterceptorBase : IInterceptor
{
    public virtual TResult Invoke<TResult>(
        IInvocation<TResult> invocation,
        Func<IInvocation<TResult>, TResult> next
    ) => next(invocation);

    public virtual void InvokeVoid(
        IInvocation<PicoDI.Abs.VoidResult> invocation,
        Action<IInvocation<PicoDI.Abs.VoidResult>> next
    ) => next(invocation);

    public virtual ValueTask<TResult> InvokeAsync<TResult>(
        IInvocation<TResult> invocation,
        Func<IInvocation<TResult>, ValueTask<TResult>> next
    ) => next(invocation);

    public virtual ValueTask InvokeAsyncVoid(
        IInvocation<PicoDI.Abs.VoidResult> invocation,
        Func<IInvocation<PicoDI.Abs.VoidResult>, ValueTask> next
    ) => next(invocation);
}
