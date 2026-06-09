namespace PicoAop.Abs;

/// <summary>
/// Base class for interceptors with pass-through default implementations.
/// Override only the methods you need.
/// </summary>
public abstract class InterceptorBase : IInterceptor
{
    /// <inheritdoc />
    public virtual void InvokeVoid<TInvocation>(TInvocation inv, Func<TInvocation, object?> next)
        where TInvocation : struct, IInvocation => next(inv);

    /// <inheritdoc />
    public virtual TResult Invoke<TInvocation, TResult>(TInvocation inv, Func<TInvocation, TResult> next)
        where TInvocation : struct, IInvocation<TResult> => next(inv);

    /// <inheritdoc />
    public virtual ValueTask InvokeAsyncVoid<TInvocation>(TInvocation inv, Func<TInvocation, ValueTask> next)
        where TInvocation : struct, IInvocation => next(inv);

    /// <inheritdoc />
    public virtual ValueTask<TResult> InvokeAsync<TInvocation, TResult>(TInvocation inv, Func<TInvocation, ValueTask<TResult>> next)
        where TInvocation : struct, IInvocation<TResult> => next(inv);
}
