namespace PicoHex.Ioc;

public class DiScope : IDisposable
{
    private readonly DiContainer _container;
    private readonly Guid _scopeId;

    public DiScope(DiContainer container)
    {
        _container = container;
        _scopeId = Guid.NewGuid();
        _container.SetScope(_scopeId);
    }

    public void Dispose()
    {
        _container.ClearScope();
    }
}
