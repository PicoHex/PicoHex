namespace PicoHex.IoC;

public class SvcScopeFactory : ISvcScopeFactory
{
    public ISvcScope CreateScope(ISvcProvider provider) => new SvcScope(provider);
}
