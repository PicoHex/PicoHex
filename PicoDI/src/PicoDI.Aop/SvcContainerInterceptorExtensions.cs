namespace PicoDI.Aop;

public static class SvcContainerInterceptorExtensions
{
    extension(ISvcContainer container)
    {
        public ISvcContainer InterceptBy<TInterceptor>()
            where TInterceptor : class, IInterceptor
        {
            throw new SourceGeneratorRequiredException(
                "PicoDI.Aop source generator must be applied. "
                + "Add PicoDI.Gen as an analyzer."
            );
        }

        public ISvcContainer InterceptBy(Type interceptorType)
        {
            throw new SourceGeneratorRequiredException(
                "PicoDI.Aop source generator must be applied."
            );
        }

        public ISvcContainer WithoutInterceptors()
        {
            throw new SourceGeneratorRequiredException(
                "PicoDI.Aop source generator must be applied."
            );
        }

        public ISvcContainer WithoutInterceptor<TInterceptor>()
            where TInterceptor : class, IInterceptor
        {
            throw new SourceGeneratorRequiredException(
                "PicoDI.Aop source generator must be applied."
            );
        }
    }
}
