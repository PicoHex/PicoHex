namespace PicoHex.DependencyInjection;

public class SvcProviderFactory(ISvcScopeFactory scopeFactory) : ISvcProviderFactory
{
    public ISvcProvider CreateServiceProvider(ISvcRegistry registry) =>
        new SvcProvider(registry, scopeFactory);
}
