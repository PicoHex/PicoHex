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
        registry.AddTransient<ISvcRegistry>(_ => registry);
        registry.AddTransient<ISvcProvider>(_ => registry.CreateProvider());
        return registry;
    }
}
