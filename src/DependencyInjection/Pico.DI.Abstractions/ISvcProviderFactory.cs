namespace Pico.DI.Abstractions;

public interface ISvcProviderFactory
{
    ISvcProvider CreateProvider(ISvcContainer container);
}
