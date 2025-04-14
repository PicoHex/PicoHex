namespace PicoHex.DI;

public class SvcResolverFactory : ISvcResolverFactory
{
    public ISvcResolver CreateResolver(ISvcContainer container) => new SvcResolver(container);
}
