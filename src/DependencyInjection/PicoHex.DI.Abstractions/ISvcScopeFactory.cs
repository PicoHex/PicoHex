namespace PicoHex.DI.Abstractions;

public interface ISvcScopeFactory
{
    ISvcScope CreateScope(ISvcContainer container, ISvcProvider provider);
}
