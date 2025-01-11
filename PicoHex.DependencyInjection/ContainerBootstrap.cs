namespace PicoHex.DependencyInjection;

public static class ContainerBootstrap
{
    public static ISvcRegistry CreateRegistry() =>
        new SvcRegistryFactory(new SvcProviderFactory(new SvcScopeFactory())).CreateRegistry();
}
