namespace PicoHex.IoC.Abstractions;

public interface ISvcContainer
{
    ISvcContainer Register(SvcDescriptor descriptor);
    ISvcProvider CreateProvider();
    SvcDescriptor? GetDescriptor(Type type);
}
