namespace PicoHex.Abstractions.DependencyInjection;

public enum SvcLifetime
{
    Singleton,
    Scoped,
    PerThread,
    Transient
}
