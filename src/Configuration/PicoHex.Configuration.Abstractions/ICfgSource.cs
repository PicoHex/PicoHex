namespace PicoHex.Configuration.Abstractions;

public interface ICfgSource
{
    ValueTask<ICfgProvider> BuildProviderAsync(CancellationToken ct = default);
}
