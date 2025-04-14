namespace PicoHex.DI.Abstractions;

public interface ISvcResolverFactory
{
    ISvcResolver CreateResolver(ISvcContainer container, ISvcProvider provider);
}
