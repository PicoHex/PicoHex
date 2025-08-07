namespace Pico.DI.Abs;

public interface ISvcScopeFactory
{
    ISvcScope CreateScope(ISvcContainer container, ISvcProvider provider);
}
