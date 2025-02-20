namespace PicoHex.Abstractions.DependencyInjection;

public interface ISvcScopeFactory
{
    ISvcScope CreateScope(ISvcProvider provider);
}
