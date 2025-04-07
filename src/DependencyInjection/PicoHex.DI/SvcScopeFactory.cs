namespace PicoHex.DI;

public sealed class SvcScopeFactory(ISvcContainer container, ISvcProvider provider)
    : ISvcScopeFactory
{
    public ISvcScope CreateScope() => new SvcScope(container, provider);
}
