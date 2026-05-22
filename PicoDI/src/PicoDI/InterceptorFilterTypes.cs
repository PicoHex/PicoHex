namespace PicoDI;

internal interface IInterceptorFilter
{
    Type InterceptorType { get; }
    bool Matches(Type serviceType);
}

internal sealed class NamespaceFilter(Type interceptorType, string ns) : IInterceptorFilter
{
    public Type InterceptorType { get; } = interceptorType;

    public bool Matches(Type serviceType) => serviceType.Namespace == ns;
}

internal sealed class InterfaceFilter(Type interceptorType, Type iface) : IInterceptorFilter
{
    public Type InterceptorType { get; } = interceptorType;

    public bool Matches(Type serviceType) => iface.IsAssignableFrom(serviceType);
}

internal sealed class AttributeFilter(Type interceptorType, Type attr) : IInterceptorFilter
{
    public Type InterceptorType { get; } = interceptorType;

    public bool Matches(Type serviceType) =>
        serviceType.GetCustomAttributes(attr, inherit: true).Length > 0;
}

internal interface IInterceptorExclusion
{
    Type InterceptorType { get; }
    Type ServiceType { get; }
}

internal sealed class ExclusionEntry(Type interceptorType, Type serviceType) : IInterceptorExclusion
{
    public Type InterceptorType { get; } = interceptorType;
    public Type ServiceType { get; } = serviceType;
}
