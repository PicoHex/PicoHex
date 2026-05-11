namespace PicoCfg.Abs;

/// <summary>
/// Provides configuration snapshots from a single source.
/// <see cref="Snapshot"/> must always return a stable, concurrently readable snapshot instance.
/// Snapshot reference identity is the provider's published version identity:
/// when <see cref="ReloadAsync"/> returns <see langword="true"/>, <see cref="Snapshot"/> must be a
/// different instance than before the reload; when it returns <see langword="false"/>, the same
/// snapshot instance must be retained.
/// The provider exposes a one-shot change signal for its current published snapshot version
/// through internal infrastructure used by the configuration root.
///
/// Returning <see langword="false"/> from <see cref="ReloadAsync"/> is authoritative: the provider's
/// published snapshot version is unchanged, and callers may retain the current <see cref="Snapshot"/>
/// reference without re-reading it.
/// Returning <see langword="true"/> indicates that the provider may have published a new snapshot
/// version, so callers should re-sample <see cref="Snapshot"/>.
/// </summary>
internal interface ICfgProvider : IAsyncDisposable
{
    ICfgSnapshot Snapshot { get; }
    ValueTask<bool> ReloadAsync(CancellationToken ct = default);
}
