namespace PicoHex.Logger.NG.Abstractions;

public interface ILogScope : IDisposable
{
    object? State { get; }
    IReadOnlyList<ILogScope> ActiveScopes { get; }
}
