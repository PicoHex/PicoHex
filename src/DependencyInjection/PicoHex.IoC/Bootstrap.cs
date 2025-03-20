namespace PicoHex.IoC;

public static class Bootstrap
{
    public static ISvcContainer CreateContainer()
    {
        var container = new SvcContainerFactory(
            new SvcProviderFactory(new SvcScopeFactory())
        ).CreateContainer();
        container.RegisterSingle<ISvcContainer>(container);
        container.RegisterTransient<ISvcProviderFactory, SvcProviderFactory>();
        container.RegisterTransient<ISvcScopeFactory, SvcScopeFactory>();
        container.RegisterTransient<ISvcProvider>(sp =>
            sp.Resolve<ISvcProviderFactory>()!.CreateProvider(container)
        );
        return container;
    }
}
