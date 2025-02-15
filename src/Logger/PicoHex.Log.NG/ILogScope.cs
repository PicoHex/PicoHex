namespace PicoHex.Log.NG;

public interface ILogScope : IDisposable
{
    object State { get; }
}
