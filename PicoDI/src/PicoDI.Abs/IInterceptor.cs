namespace PicoDI.Abs;

public interface IInterceptor
{
    TResult Invoke<TResult>(
        IInvocation<TResult> invocation,
        Func<IInvocation<TResult>, TResult> next
    );

    void InvokeVoid(IInvocation<VoidResult> invocation, Action<IInvocation<VoidResult>> next);

    ValueTask<TResult> InvokeAsync<TResult>(
        IInvocation<TResult> invocation,
        Func<IInvocation<TResult>, ValueTask<TResult>> next
    );

    ValueTask InvokeAsyncVoid(
        IInvocation<VoidResult> invocation,
        Func<IInvocation<VoidResult>, ValueTask> next
    );
}
