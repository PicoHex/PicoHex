namespace PicoDI.Abs;

public abstract class InterceptorBase : IInterceptor
{
    public virtual TResult Invoke<TResult>(
        IInvocation<TResult> invocation,
        Func<IInvocation<TResult>, TResult> next
    ) => next(invocation);

    public virtual void InvokeVoid(
        IInvocation<VoidResult> invocation,
        Action<IInvocation<VoidResult>> next
    ) => next(invocation);

    public virtual ValueTask<TResult> InvokeAsync<TResult>(
        IInvocation<TResult> invocation,
        Func<IInvocation<TResult>, ValueTask<TResult>> next
    ) => next(invocation);

    public virtual ValueTask InvokeAsyncVoid(
        IInvocation<VoidResult> invocation,
        Func<IInvocation<VoidResult>, ValueTask> next
    ) => next(invocation);
}
