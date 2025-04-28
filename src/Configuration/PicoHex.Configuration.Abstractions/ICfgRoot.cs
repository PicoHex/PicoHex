namespace PicoHex.Configuration.Abstractions;

public interface ICfgRoot : ICfgNode
{
    ValueTask ReloadAsync(CancellationToken ct = default);
    IReadOnlyList<ICfgProvider> Providers { get; }
}
