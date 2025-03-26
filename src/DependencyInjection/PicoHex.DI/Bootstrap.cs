namespace PicoHex.DI;

public static class Bootstrap
{
    public static ISvcContainer CreateContainer()
    {
        var container = new SvcContainerFactory(
            new SvcProviderFactory(new SvcScopeFactory())
        ).CreateContainer();
        container.RegisterSingle<ISvcContainer>(container);
        container.RegisterSingle<ISvcProviderFactory, SvcProviderFactory>();
        container.RegisterSingle<ISvcScopeFactory, SvcScopeFactory>();
        container.RegisterScoped<ISvcProvider>(
            sp => sp.Resolve<ISvcProviderFactory>()!.CreateProvider(container)
        );
        container.RegisterScoped<ISvcScope>(
            sp => sp.Resolve<ISvcScopeFactory>()!.CreateScope(sp.Resolve<ISvcProvider>()!)
        );
        return container;
    }
}
