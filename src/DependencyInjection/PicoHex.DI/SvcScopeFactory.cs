namespace PicoHex.DI;

public sealed class SvcScopeFactory : ISvcScopeFactory
{
    public ISvcScope CreateScope(
        ISvcContainer container,
        ISvcProvider provider,
        ISvcResolverFactory resolverFactory
    ) => new SvcScope(container, provider, resolverFactory);
}
