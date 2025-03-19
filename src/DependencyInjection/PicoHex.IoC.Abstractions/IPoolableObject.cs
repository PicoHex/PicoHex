namespace PicoHex.IoC.Abstractions;

public interface IPoolableObject : IDisposable
{
    void Reset();
}
