namespace PicoHex.DependencyInjection;

public class SvcProvider(ISvcRegistry registry, ISvcScopeFactory scopeFactory) : ISvcProvider
{
    public object? Resolve(Type implementationType) =>
        registry.GetInstanceFactory(implementationType)(this);

    public ISvcScope CreateScope() => scopeFactory.CreateScope(this);
}
