namespace PicoHex.DependencyInjection;

public class SvcRegistryFactory(ISvcProviderFactory providerFactory) : ISvcRegistryFactory
{
    public ISvcRegistry CreateRegistry() => new SvcRegistry(providerFactory);
}
