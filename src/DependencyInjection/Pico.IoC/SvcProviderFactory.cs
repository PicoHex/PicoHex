namespace Pico.IoC;

public sealed class SvcProviderFactory(
    ISvcScopeFactory scopeFactory,
    ISvcResolverFactory resolverFactory
) : ISvcProviderFactory
{
    public ISvcProvider CreateProvider(ISvcContainer container) =>
        new SvcProvider(container, scopeFactory, resolverFactory);
}
