namespace PicoHex.Core.DependencyInjection;

public interface ISvcScopeFactory
{
    ISvcScope CreateScope(ISvcProvider provider);
}
