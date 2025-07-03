namespace Pico.DI.Abs;

public interface ISvcProviderFactory
{
    ISvcProvider CreateProvider(ISvcContainer container);
}
