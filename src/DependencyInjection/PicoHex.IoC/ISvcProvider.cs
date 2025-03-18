namespace PicoHex.IoC;

public interface ISvcProvider
{
    object GetService(Type serviceType);
}
