using System;

namespace PicoAop.DI;

/// <summary>
/// Compile-time interceptor registration methods for <see cref="PicoDI.Abs.ISvcContainer"/>.
/// These methods are compile-time markers detected by <c>PicoAop.Gen</c> at build time.
/// At runtime they throw <see cref="InvalidOperationException"/> — the
/// <c>PicoAop.Gen</c> source generator must be referenced for interceptors to take effect.
/// </summary>
public static class SvcContainerInterceptorExtensions
{
    private const string GeneratorRequired =
        "The PicoAop.Gen source generator must be referenced. "
        + "These methods are compile-time markers and require PicoAop.Gen to generate decorator classes.";

    extension(ISvcContainer container)
    {
        /// <summary>
        /// Declares <typeparamref name="TInterceptor"/> as an interceptor on the
        /// preceding <c>Register</c> call. Detected by <c>PicoAop.Gen</c> at compile time.
        /// Throws at runtime if the source generator is not referenced.
        /// </summary>
        public ISvcContainer InterceptBy<TInterceptor>()
            where TInterceptor : class, IInterceptor
        {
            throw new InvalidOperationException(GeneratorRequired);
        }

        /// <summary>
        /// Runtime overload — always throws; only the generic overload is
        /// processed by the source generator.
        /// </summary>
        public ISvcContainer InterceptBy(Type interceptorType)
        {
            throw new InvalidOperationException(GeneratorRequired);
        }

        /// <summary>
        /// Removes all interceptors (both per-service and global) from the
        /// preceding <c>Register</c> call. Detected by <c>PicoAop.Gen</c> at compile time.
        /// </summary>
        public ISvcContainer WithoutInterceptors()
        {
            throw new InvalidOperationException(GeneratorRequired);
        }

        /// <summary>
        /// Excludes <typeparamref name="TInterceptor"/> from the preceding
        /// <c>Register</c> call. Detected by <c>PicoAop.Gen</c> at compile time.
        /// </summary>
        public ISvcContainer WithoutInterceptor<TInterceptor>()
            where TInterceptor : class, IInterceptor
        {
            throw new InvalidOperationException(GeneratorRequired);
        }
    }

    /// <summary>
    /// Registers <typeparamref name="TInterceptor"/> as a global interceptor
    /// for all services. Detected by <c>PicoAop.Gen</c> at compile time.
    /// Throws at runtime if the source generator is not referenced.
    /// </summary>
    public static ISvcContainer AddInterceptor<TInterceptor>(this ISvcContainer container)
        where TInterceptor : class, IInterceptor
    {
        throw new InvalidOperationException(GeneratorRequired);
    }
}
