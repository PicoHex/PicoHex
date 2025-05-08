namespace Pico.Log.Abstractions;

public interface ILogScope : IDisposable
{
    object State { get; }
}
