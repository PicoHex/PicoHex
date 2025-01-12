namespace PicoHex.DependencyInjection.Abstractions;

public interface ISvcRegistry
{
    SvcDescriptor GetServiceDescriptor(Type serviceType);
    ISvcRegistry AddServiceDescriptor(SvcDescriptor descriptor);
    Func<ISvcProvider, object?> GetInstanceFactory(Type serviceType);
    object? GetSingletonInstance(Type type, Func<object?> instanceFactory);
    object? GetPerThreadInstance(Type type, Func<object?> instanceFactory);
    ISvcProvider CreateProvider();
}
