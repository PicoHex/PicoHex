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
        container.RegisterSingle<ISvcProvider>(sp => sp.Resolve<ISvcContainer>().CreateProvider());
        return container;
    }
}
