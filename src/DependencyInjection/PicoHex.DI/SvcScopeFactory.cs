namespace PicoHex.DI;

public sealed class SvcScopeFactory : ISvcScopeFactory
{
    public ISvcScope CreateScope(ISvcProvider provider) => new SvcScope(provider);
}
