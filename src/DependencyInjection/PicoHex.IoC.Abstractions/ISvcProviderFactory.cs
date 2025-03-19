namespace PicoHex.IoC.Abstractions;

public interface ISvcProviderFactory
{
    ISvcProvider CreateProvider(ISvcContainer container);
}
