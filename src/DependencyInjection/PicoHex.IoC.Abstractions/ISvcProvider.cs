namespace PicoHex.IoC.Abstractions;

public interface ISvcProvider
{
    object GetService(Type serviceType);
}
