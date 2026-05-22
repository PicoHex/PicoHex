namespace PicoDI;

public static class SvcContainerInterceptorExtensions
{
    extension(ISvcContainer container)
    {
        public ISvcContainer InterceptBy<TInterceptor>()
            where TInterceptor : class, IInterceptor
        {
            // Runtime no-op: the PicoDI.Gen source generator detects this call
            // at compile time and generates decorator types + DI registrations.
            return container;
        }

        public ISvcContainer InterceptBy(Type interceptorType)
        {
            return container;
        }

        public ISvcContainer WithoutInterceptors()
        {
            return container;
        }

        public ISvcContainer WithoutInterceptor<TInterceptor>()
            where TInterceptor : class, IInterceptor
        {
            return container;
        }
    }
}
