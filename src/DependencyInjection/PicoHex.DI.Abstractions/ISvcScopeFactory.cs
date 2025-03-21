namespace PicoHex.DI.Abstractions;

public interface ISvcScopeFactory
{
    ISvcScope CreateScope(ISvcProvider provider);
}
