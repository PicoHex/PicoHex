namespace PicoHex.IoC;

public sealed class SvcProviderFactory(ISvcScopeFactory scopeFactory) : ISvcProviderFactory
{
    public ISvcProvider CreateProvider(ISvcContainer container) =>
        new SvcProvider(container, scopeFactory);
}
