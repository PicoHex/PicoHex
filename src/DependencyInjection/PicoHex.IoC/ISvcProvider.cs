namespace PicoHex.IoC;

public interface ISvcProvider
{
    object Resolve(Type serviceType);
}
