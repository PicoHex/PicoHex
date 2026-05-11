namespace PicoCfg.Abs;

/// <summary>
/// Represents an immutable configuration snapshot that is safe to read concurrently.
/// Providers should replace the snapshot instance when the underlying values change.
/// The contract intentionally exposes exact lookup only. Native PicoCfg snapshots may be flattened into
/// a single dictionary-backed root snapshot, while custom <see cref="ICfgSnapshot"/> implementations may
/// be composed via read-time fallback lookups to preserve custom lookup behavior.
/// </summary>
internal interface ICfgSnapshot : ICfg;
