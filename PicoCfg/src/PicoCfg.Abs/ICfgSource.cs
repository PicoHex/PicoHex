namespace PicoCfg.Abs;

/// <summary>
/// Opens a configuration source into a long-lived provider.
/// Implementations must return a provider whose <see cref="ICfgProvider.Snapshot"/> is already
/// initialized and safe to read immediately after <see cref="OpenAsync"/> completes.
/// </summary>
internal interface ICfgSource
{
    ValueTask<ICfgProvider> OpenAsync(CancellationToken ct = default);
}
