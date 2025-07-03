namespace Pico.DI.Abs;

public interface ISvcResolverFactory
{
    ISvcResolver CreateResolver(ISvcContainer container, ISvcProvider provider);
}
