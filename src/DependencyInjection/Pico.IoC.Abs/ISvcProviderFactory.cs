namespace Pico.IoC.Abs;

public interface ISvcProviderFactory
{
    ISvcProvider CreateProvider(ISvcContainer container);
}
