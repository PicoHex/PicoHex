namespace PicoHex.Core.DependencyInjection;

public enum SvcLifetime
{
    Singleton,
    Scoped,
    PerThread,
    Transient,
    Pooled
}
