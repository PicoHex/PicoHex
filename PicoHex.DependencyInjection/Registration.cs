namespace PicoHex.DependencyInjection;

internal class Registration
{
    public Func<DiContainer, object> Factory { get; set; }
    public Lifetime Lifetime { get; set; }
    public object SingletonInstance { get; set; }
    public ThreadLocal<object> ThreadInstance { get; set; } = new ThreadLocal<object>(() => null);
    public ConcurrentDictionary<Guid, object> ScopedInstances { get; set; } =
        new ConcurrentDictionary<Guid, object>();
}
