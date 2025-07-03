namespace Pico.IoC.Abs;

public interface ISvcResolverFactory
{
    ISvcResolver CreateResolver(ISvcContainer container, ISvcProvider provider);
}
