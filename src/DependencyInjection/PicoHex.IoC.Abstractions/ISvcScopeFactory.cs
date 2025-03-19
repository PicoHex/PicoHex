namespace PicoHex.IoC.Abstractions;

public interface ISvcScopeFactory
{
    ISvcScope CreateScope(ISvcProvider provider);
}
