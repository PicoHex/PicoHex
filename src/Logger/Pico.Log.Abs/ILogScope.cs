namespace Pico.Log.Abs;

public interface ILogScope : IDisposable
{
    object State { get; }
}
