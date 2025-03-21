namespace PicoHex.Log.Abstractions;

public interface ILogScope : IDisposable
{
    object State { get; }
}
