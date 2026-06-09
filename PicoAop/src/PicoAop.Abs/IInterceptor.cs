namespace PicoAop.Abs;

/// <summary>
/// AOT-first interceptor interface with zero-allocation invocation paths.
/// All methods use struct generics with cached static delegates — no boxing, no reflection.
/// </summary>
public interface IInterceptor
{
    // ── Synchronous ──

    /// <summary>
    /// Intercepts a synchronous void-returning method call.
    /// </summary>
    void InvokeVoid<TInvocation>(TInvocation inv, Func<TInvocation, object?> next)
        where TInvocation : struct, IInvocation;

    /// <summary>
    /// Intercepts a synchronous method call returning <typeparamref name="TResult"/>.
    /// </summary>
    TResult Invoke<TInvocation, TResult>(TInvocation inv, Func<TInvocation, TResult> next)
        where TInvocation : struct, IInvocation<TResult>;

    // ── Asynchronous ──

    /// <summary>
    /// Intercepts an asynchronous void-returning method call.
    /// </summary>
    ValueTask InvokeAsyncVoid<TInvocation>(TInvocation inv, Func<TInvocation, ValueTask> next)
        where TInvocation : struct, IInvocation;

    /// <summary>
    /// Intercepts an asynchronous method call returning <typeparamref name="TResult"/>.
    /// </summary>
    ValueTask<TResult> InvokeAsync<TInvocation, TResult>(
        TInvocation inv,
        Func<TInvocation, ValueTask<TResult>> next
    )
        where TInvocation : struct, IInvocation<TResult>;
}
