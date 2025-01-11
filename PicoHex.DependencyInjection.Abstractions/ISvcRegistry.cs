namespace PicoHex.DependencyInjection.Abstractions;

public interface ISvcRegistry
{
    SvcDescriptor GetServiceDescriptor(Type type);
    ISvcRegistry AddServiceDescriptor(SvcDescriptor descriptor);
    object? GetSingletonInstance(Type type, Func<object?> instanceFactory);
    object? GetPerThreadInstance(Type type, Func<object?> instanceFactory);
    ISvcProvider CreateProvider();
}
