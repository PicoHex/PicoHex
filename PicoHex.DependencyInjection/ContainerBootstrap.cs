namespace PicoHex.DependencyInjection;

public static class ContainerBootstrap
{
    public static ISvcRegistry CreateRegistry()
    {
        var registry = new SvcRegistryFactory(
            new SvcProviderFactory(new SvcScopeFactory())
        ).CreateRegistry();
        registry.AddTransient<ISvcRegistryFactory, SvcRegistryFactory>();
        registry.AddTransient<ISvcProviderFactory, SvcProviderFactory>();
        registry.AddTransient<ISvcScopeFactory, SvcScopeFactory>();
        var provider = registry.CreateProvider();
        var registryFactory = provider.Resolve<ISvcRegistryFactory>();
        return registryFactory.CreateRegistry();
    }
}
