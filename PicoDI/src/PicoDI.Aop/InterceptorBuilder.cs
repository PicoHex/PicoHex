namespace PicoDI.Aop;

public sealed class InterceptorBuilder
{
    private readonly SvcContainer _container;
    private readonly Type _interceptorType;

    internal InterceptorBuilder(SvcContainer container, Type interceptorType)
    {
        _container = container;
        _interceptorType = interceptorType;
    }

    public InterceptorBuilder WhereNamespace(string ns)
    {
        _container.InterceptorFilters.Add(new NamespaceFilter(_interceptorType, ns));
        return this;
    }

    public InterceptorBuilder WhereImplements<TInterface>()
    {
        _container
            .InterceptorFilters
            .Add(new InterfaceFilter(_interceptorType, typeof(TInterface)));
        return this;
    }

    public InterceptorBuilder WhereHasAttribute<TAttribute>()
    {
        _container
            .InterceptorFilters
            .Add(new AttributeFilter(_interceptorType, typeof(TAttribute)));
        return this;
    }

    public InterceptorBuilder Except<TService>()
    {
        _container
            .InterceptorExclusions
            .Add(new ExclusionEntry(_interceptorType, typeof(TService)));
        return this;
    }
}

public static class SvcContainerAddInterceptorExtensions
{
    public static InterceptorBuilder AddInterceptor<TInterceptor>(this ISvcContainer container)
        where TInterceptor : class, IInterceptor
    {
        if (container is not SvcContainer sc)
            throw new InvalidOperationException("AddInterceptor requires a SvcContainer instance.");

        return new InterceptorBuilder(sc, typeof(TInterceptor));
    }
}
