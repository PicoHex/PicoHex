namespace PicoDI;

public static class SvcContainerAddInterceptorExtensions
{
    public static ISvcContainer AddInterceptor<TInterceptor>(this ISvcContainer container)
        where TInterceptor : class, IInterceptor
    {
        return container;
    }
}
