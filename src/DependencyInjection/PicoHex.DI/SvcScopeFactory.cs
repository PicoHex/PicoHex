namespace PicoHex.DI;

public sealed class SvcScopeFactory : ISvcScopeFactory
{
    public ISvcScope CreateScope(ISvcContainer container, ISvcProvider provider) =>
        new SvcScope(container, provider);
}
