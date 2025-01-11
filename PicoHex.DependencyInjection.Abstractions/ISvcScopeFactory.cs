namespace PicoHex.DependencyInjection.Abstractions;

public interface ISvcScopeFactory
{
    ISvcScope CreateScope(ISvcProvider provider);
}
