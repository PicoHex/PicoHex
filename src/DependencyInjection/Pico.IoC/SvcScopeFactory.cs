namespace Pico.IoC;

public sealed class SvcScopeFactory(ISvcResolverFactory resolverFactory) : ISvcScopeFactory
{
    public ISvcScope CreateScope(ISvcContainer container, ISvcProvider provider) =>
        new SvcScope(container, provider, resolverFactory);
}
