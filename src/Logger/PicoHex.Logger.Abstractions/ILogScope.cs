namespace PicoHex.Logger.Abstractions;

public interface ILogScope : IDisposable
{
    object? State { get; }
    // IReadOnlyList<ILogScope> ActiveScopes { get; }
}
