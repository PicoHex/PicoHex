namespace PicoCfg.Abs;

/// <summary>
/// Represents the composed configuration root across all registered providers.
/// The root always exposes the currently published exact-lookup configuration view through <see cref="ICfg"/>.
/// <see cref="ReloadAsync"/> publishes at most one new composed configuration view per call.
/// When it returns <see langword="false"/>, no provider published a new version and the current
/// published configuration view is retained.
/// If <see cref="ReloadAsync"/> throws or is canceled after one or more providers have already published
/// new versions, the root may first publish the observed composed configuration view for those settled
/// provider versions and then rethrow the failure.
/// <see cref="WaitForChangeAsync"/> waits for the next published change.
/// </summary>
public interface ICfgRoot : ICfg, IAsyncDisposable
{
    ValueTask<bool> ReloadAsync(CancellationToken ct = default);
    ValueTask WaitForChangeAsync(CancellationToken ct = default);
}
