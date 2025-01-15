namespace PicoHex.DependencyInjection.Abstractions;

public enum SvcLifetime
{
    Singleton,
    Scoped,
    PerThread,
    Transient
}
