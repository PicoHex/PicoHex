namespace Pico.IoC.Abs;

public interface ISvcScopeFactory
{
    ISvcScope CreateScope(ISvcContainer container, ISvcProvider provider);
}
