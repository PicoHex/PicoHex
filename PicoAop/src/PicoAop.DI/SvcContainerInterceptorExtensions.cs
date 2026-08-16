namespace PicoAop.DI;

/// <summary>
/// Compile-time interceptor registration methods for <see cref="PicoDI.Abs.ISvcContainer"/>.
/// These methods are compile-time markers detected by <c>PicoAop.Gen</c> at build time.
/// At runtime they are no-ops: <c>PicoAop.Gen</c> generates the decorator types and
/// <c>PicoDI.Gen</c> rewrites the registrations, so the marker calls never need to
/// perform real work. When the generators are missing, the failure is surfaced by
/// <c>PicoDI.Gen</c> emitting unresolved wrapper references (build error) or by the
/// PicoDI <c>Register*</c> marker methods throwing <c>SourceGeneratorRequiredException</c>.
/// </summary>
public static class SvcContainerInterceptorExtensions
{
    extension(ISvcContainer container)
    {
        /// <summary>
        /// Declares <typeparamref name="TInterceptor"/> as an interceptor on the
        /// preceding <c>Register</c> call. Detected by <c>PicoAop.Gen</c> at compile time.
        /// Runtime no-op.
        /// </summary>
        public ISvcContainer InterceptBy<TInterceptor>()
            where TInterceptor : class, IInterceptor
        {
            // Runtime no-op: the PicoAop.Gen source generator detects this call
            // at compile time and generates decorator types + DI registrations.
            return container;
        }

        /// <summary>
        /// Runtime overload — not processed by the source generator. Runtime no-op.
        /// </summary>
        public ISvcContainer InterceptBy(Type interceptorType)
        {
            return container;
        }

        /// <summary>
        /// Removes all interceptors (both per-service and global) from the
        /// preceding <c>Register</c> call. Detected by <c>PicoAop.Gen</c> at compile time.
        /// Runtime no-op.
        /// </summary>
        public ISvcContainer WithoutInterceptors()
        {
            return container;
        }

        /// <summary>
        /// Excludes <typeparamref name="TInterceptor"/> from the preceding
        /// <c>Register</c> call. Detected by <c>PicoAop.Gen</c> at compile time.
        /// Runtime no-op.
        /// </summary>
        public ISvcContainer WithoutInterceptor<TInterceptor>()
            where TInterceptor : class, IInterceptor
        {
            return container;
        }
    }

    /// <summary>
    /// Registers <typeparamref name="TInterceptor"/> as a global interceptor
    /// for all services. Detected by <c>PicoAop.Gen</c> at compile time.
    /// Runtime no-op.
    /// </summary>
    public static ISvcContainer AddInterceptor<TInterceptor>(this ISvcContainer container)
        where TInterceptor : class, IInterceptor
    {
        return container;
    }
}
