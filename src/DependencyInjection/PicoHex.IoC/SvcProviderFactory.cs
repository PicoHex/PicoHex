namespace PicoHex.IoC;

public class SvcProviderFactory(ISvcScopeFactory scopeFactory) : ISvcProviderFactory
{
    public ISvcProvider CreateProvider(ISvcContainer container) =>
        new SvcProvider(container, scopeFactory);
}
