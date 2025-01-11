namespace PicoHex.DependencyInjection;

public class SvcScopeFactory : ISvcScopeFactory
{
    public ISvcScope CreateScope(ISvcProvider provider) => new SvcScope(provider);
}
