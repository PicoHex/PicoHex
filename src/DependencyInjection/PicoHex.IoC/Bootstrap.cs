namespace PicoHex.IoC;

public static class Bootstrap
{
    public static ISvcContainer CreateContainer()
    {
        var container = new SvcContainerFactory(
            new SvcProviderFactory(new SvcScopeFactory())
        ).CreateContainer();
        container.RegisterTransient<ISvcProviderFactory, SvcProviderFactory>();
        container.RegisterTransient<ISvcScopeFactory, SvcScopeFactory>();
        container.RegisterTransient<ISvcProvider>(_ => container.CreateProvider());
        return container;
    }
}
