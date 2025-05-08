namespace Pico.DI;

public static class Bootstrap
{
    public static ISvcContainer CreateContainer()
    {
        var svcResolverFactory = new SvcResolverFactory();
        var svcScopeFactory = new SvcScopeFactory(svcResolverFactory);
        var svcProviderFactory = new SvcProviderFactory(svcScopeFactory, svcResolverFactory);
        var containerFactory = new SvcContainerFactory(svcProviderFactory);
        var container = containerFactory.CreateContainer();

        return container
            .RegisterSingle<ISvcContainer>(container)
            .RegisterSingle<ISvcProviderFactory>(svcProviderFactory)
            .RegisterSingle<ISvcScopeFactory>(svcScopeFactory)
            .RegisterSingle<ISvcResolverFactory>(svcResolverFactory)
            .RegisterSingle<ISvcProvider>(container.GetProvider())
            .RegisterScoped<ISvcScope>(sp =>
                sp.Resolve<ISvcScopeFactory>().CreateScope(container, sp)
            );
    }
}
