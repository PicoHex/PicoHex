namespace PicoHex.DI;

public static class Bootstrap
{
    public static ISvcContainer CreateContainer()
    {
        var container = new SvcContainerFactory(
            new SvcProviderFactory(new SvcScopeFactory())
        ).CreateContainer();

        return container
            .RegisterSingle<ISvcContainer>(container)
            .RegisterSingle<ISvcProviderFactory, SvcProviderFactory>()
            .RegisterSingle<ISvcScopeFactory, SvcScopeFactory>()
            .RegisterSingle<ISvcProvider>(sp => sp.Resolve<ISvcContainer>().CreateProvider());
    }
}
