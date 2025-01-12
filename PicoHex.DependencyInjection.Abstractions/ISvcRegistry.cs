namespace PicoHex.DependencyInjection.Abstractions;

public interface ISvcRegistry
{
    SvcDescriptor GetServiceDescriptor(Type type);
    ISvcRegistry AddServiceDescriptor(SvcDescriptor descriptor);
    Func<ISvcProvider, object?> GetInstanceFactory(Type implementationType);
    object? GetSingletonInstance(Type type, Func<object?> instanceFactory);
    object? GetPerThreadInstance(Type type, Func<object?> instanceFactory);
    ISvcProvider CreateProvider();
}
